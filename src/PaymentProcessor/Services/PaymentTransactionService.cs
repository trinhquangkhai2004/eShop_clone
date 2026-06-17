using eShop.PaymentProcessor.Domain;
using eShop.PaymentProcessor.Infrastructure;

namespace eShop.PaymentProcessor.Services;

public class PaymentTransactionService(PaymentDbContext dbContext) : IPaymentTransactionService
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
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkFailed();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkNeedReviewAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkNeedReview();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordReconciliationAttemptAsync(
        PaymentTransaction transaction,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        transaction.IncrementReconciliationAttempt(maxAttempts);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkResultPublishedAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.MarkResultPublished();
        await dbContext.SaveChangesAsync(cancellationToken);
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
                (t.Status == PaymentTransactionStatus.Succeeded || t.Status == PaymentTransactionStatus.Failed)
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
                t => t.IdempotencyKey == idempotencyKey || t.OrderId == orderId,
                cancellationToken);
    }
}
