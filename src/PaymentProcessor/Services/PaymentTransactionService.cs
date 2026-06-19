using eShop.PaymentProcessor.Domain;
using eShop.PaymentProcessor.Infrastructure;

namespace eShop.PaymentProcessor.Services;

public class PaymentTransactionService(
    PaymentDbContext dbContext,
    IIntegrationEventLogService? integrationEventLogService = null) : IPaymentTransactionService
{
    public async Task<CreateOrGetPaymentTransactionResult> CreateOrGetAsync(
        int orderId,
        string? userId,
        decimal amount,
        string currency,
        string paymentMethod,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var existingTransaction = await FindExistingTransactionAsync(orderId, idempotencyKey, cancellationToken);
        if (existingTransaction is not null)
        {
            return new CreateOrGetPaymentTransactionResult(existingTransaction, IsIdempotencyHit: true);
        }

        var transaction = new PaymentTransaction(
            orderId,
            userId,
            amount,
            currency,
            paymentMethod,
            idempotencyKey);

        dbContext.PaymentTransactions.Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(transaction).State = EntityState.Detached;

            existingTransaction = await FindExistingTransactionAsync(orderId, idempotencyKey, cancellationToken);
            if (existingTransaction is not null)
            {
                return new CreateOrGetPaymentTransactionResult(existingTransaction, IsIdempotencyHit: true);
            }

            throw;
        }

        return new CreateOrGetPaymentTransactionResult(transaction, IsIdempotencyHit: false);
    }

    public async Task MarkProcessingAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkProcessing();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSucceededAsync(
        PaymentTransaction transaction,
        string gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        transaction.MarkSucceeded(gatewayTransactionId);
        await SaveTerminalStatusWithOutboxAsync(transaction, cancellationToken);
    }

    public async Task MarkFailedAsync(
        PaymentTransaction transaction,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        transaction.MarkFailed(failureReason);
        await SaveTerminalStatusWithOutboxAsync(transaction, cancellationToken);
    }

    public async Task MarkNeedReviewAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkNeedReview();
        await SaveTerminalStatusWithOutboxAsync(transaction, cancellationToken);
    }

    public async Task MarkExpiredAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkExpired();
        await SaveTerminalStatusWithOutboxAsync(transaction, cancellationToken);
    }

    public async Task RecordReconciliationAttemptAsync(
        PaymentTransaction transaction,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        transaction.IncrementReconciliationAttempt(maxAttempts);
        if (transaction.Status == PaymentTransactionStatus.NeedReview)
        {
            await SaveTerminalStatusWithOutboxAsync(transaction, cancellationToken);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkResultPublishedAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkResultPublished();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PaymentTransaction?> FindByIdAsync(
        int paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == paymentTransactionId, cancellationToken);
    }

    public Task<PaymentTransaction?> FindByGatewayTransactionIdAsync(
        string gatewayTransactionId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == gatewayTransactionId, cancellationToken);
    }

    public Task<PaymentTransaction?> FindByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.OrderId == orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> FindNeedReviewAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.Status == PaymentTransactionStatus.NeedReview)
            .OrderBy(t => t.UpdatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByStatusAsync(
        PaymentTransactionStatus status,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PaymentTransactions
            .CountAsync(t => t.Status == status, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> FindPendingForReconciliationAsync(
        DateTime staleBefore,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PaymentTransactions
            .Where(t =>
                (t.Status == PaymentTransactionStatus.Pending || t.Status == PaymentTransactionStatus.Processing)
                && t.UpdatedAt < staleBefore)
            .OrderBy(t => t.UpdatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> FindUnpublishedTerminalResultsAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PaymentTransactions
            .Where(t =>
                (t.Status == PaymentTransactionStatus.Succeeded
                    || t.Status == PaymentTransactionStatus.Failed
                    || t.Status == PaymentTransactionStatus.Expired
                    || t.Status == PaymentTransactionStatus.NeedReview)
                && !t.ResultEventPublished)
            .OrderBy(t => t.UpdatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    private Task<PaymentTransaction?> FindExistingTransactionAsync(
        int orderId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                t => t.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private async Task SaveTerminalStatusWithOutboxAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (integrationEventLogService is null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            transaction.MarkOutboxTransaction(dbTransaction.TransactionId);
            var integrationEvent = PaymentResultIntegrationEventFactory.Create(transaction);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (integrationEvent is not null)
            {
                await integrationEventLogService.SaveEventAsync(integrationEvent, dbTransaction);
            }

            await dbTransaction.CommitAsync(cancellationToken);
        });
    }
}
