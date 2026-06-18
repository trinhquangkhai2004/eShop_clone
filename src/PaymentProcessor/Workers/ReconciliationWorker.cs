using System.Diagnostics;
using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Workers;

public sealed class ReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ReconciliationOptions> options,
    PaymentProcessorTelemetry telemetry,
    ILogger<ReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelaySeconds = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var reconciliationOptions = options.CurrentValue;
            var processedCount = 0;

            if (reconciliationOptions.Enabled)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    processedCount = await ReconcileAsync(reconciliationOptions, stoppingToken);
                    telemetry.RecordReconciliationCycle(stopwatch.Elapsed, processedCount, "success");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    telemetry.RecordReconciliationCycle(stopwatch.Elapsed, processedCount, "failure");
                    logger.LogError(ex, "Payment reconciliation cycle failed.");
                }
            }

            idleDelaySeconds = GetNextDelaySeconds(
                reconciliationOptions,
                processedCount,
                idleDelaySeconds);

            await Task.Delay(TimeSpan.FromSeconds(idleDelaySeconds), stoppingToken);
        }
    }

    private async Task<int> ReconcileAsync(
        ReconciliationOptions reconciliationOptions,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var paymentTransactionService = scope.ServiceProvider.GetRequiredService<IPaymentTransactionService>();
        var bankGatewayClient = scope.ServiceProvider.GetRequiredService<IBankGatewayClient>();
        var resultEventPublisher = scope.ServiceProvider.GetRequiredService<IOrderPaymentResultEventPublisher>();

        var batchSize = Math.Max(1, reconciliationOptions.BatchSize);
        var staleBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, reconciliationOptions.StaleTransactionThresholdSeconds));
        var expirePendingBefore = reconciliationOptions.ExpirationThresholdSeconds > 0
            ? DateTime.UtcNow.AddSeconds(-reconciliationOptions.ExpirationThresholdSeconds)
            : (DateTime?)null;
        var maxAttempts = Math.Max(1, reconciliationOptions.MaxAttempts);

        var staleTransactions = await paymentTransactionService.FindPendingForReconciliationAsync(
            staleBefore,
            batchSize,
            cancellationToken);

        var processedCount = 0;

        foreach (var transaction in staleTransactions)
        {
            await ReconcileTransactionAsync(
                transaction,
                bankGatewayClient,
                paymentTransactionService,
                resultEventPublisher,
                expirePendingBefore,
                maxAttempts,
                cancellationToken);
            processedCount++;
        }

        var unpublishedTransactions = await paymentTransactionService.FindUnpublishedTerminalResultsAsync(
            batchSize,
            cancellationToken);

        foreach (var transaction in unpublishedTransactions)
        {
            await PublishRecoveredResultAsync(transaction, resultEventPublisher, cancellationToken);
            processedCount++;
        }

        return processedCount;
    }

    private async Task ReconcileTransactionAsync(
        PaymentTransaction transaction,
        IBankGatewayClient bankGatewayClient,
        IPaymentTransactionService paymentTransactionService,
        IOrderPaymentResultEventPublisher resultEventPublisher,
        DateTime? expirePendingBefore,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        using var activity = telemetry.StartActivity("payment.reconciliation.query_bank_status");
        SetPaymentTransactionTags(activity, transaction);
        activity?.SetTag("payment.gateway.provider", bankGatewayClient.Provider);

        try
        {
            if (ShouldExpirePendingTransaction(transaction, expirePendingBefore))
            {
                await paymentTransactionService.MarkExpiredAsync(transaction, cancellationToken);
                await resultEventPublisher.PublishAsync(transaction, cancellationToken);
                telemetry.RecordReconciliationProcessed(transaction.Status, "pending_expired");
                return;
            }

            transaction.MarkReconciled();
            var result = await bankGatewayClient.QueryStatusAsync(transaction, cancellationToken);
            activity?.SetTag("payment.gateway.status", result.Status.ToString());

            switch (result.Status)
            {
                case BankGatewayPaymentStatus.Succeeded when !string.IsNullOrWhiteSpace(result.GatewayTransactionId):
                    await paymentTransactionService.MarkSucceededAsync(
                        transaction,
                        result.GatewayTransactionId,
                        cancellationToken);
                    await resultEventPublisher.PublishAsync(transaction, cancellationToken);
                    telemetry.RecordReconciliationProcessed(transaction.Status, "bank_status_succeeded");
                    break;

                case BankGatewayPaymentStatus.Failed:
                    await paymentTransactionService.MarkFailedAsync(
                        transaction,
                        result.FailureReason ?? result.Status.ToString(),
                        cancellationToken);
                    await resultEventPublisher.PublishAsync(transaction, cancellationToken);
                    telemetry.RecordReconciliationProcessed(transaction.Status, "bank_status_failed");
                    break;

                default:
                    await paymentTransactionService.RecordReconciliationAttemptAsync(
                        transaction,
                        maxAttempts,
                        cancellationToken);
                    await PublishNeedReviewIfRequiredAsync(
                        transaction,
                        resultEventPublisher,
                        cancellationToken);
                    telemetry.RecordReconciliationProcessed(transaction.Status, "bank_status_unresolved");
                    break;
            }
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            telemetry.RecordFailure(ex.GetType().Name, transaction.PaymentMethod);

            await paymentTransactionService.RecordReconciliationAttemptAsync(
                transaction,
                maxAttempts,
                cancellationToken);
            await PublishNeedReviewIfRequiredAsync(
                transaction,
                resultEventPublisher,
                cancellationToken);
        }
    }

    private async Task PublishRecoveredResultAsync(
        PaymentTransaction transaction,
        IOrderPaymentResultEventPublisher resultEventPublisher,
        CancellationToken cancellationToken)
    {
        using var activity = telemetry.StartActivity("payment.reconciliation.publish_recovery");
        SetPaymentTransactionTags(activity, transaction);

        try
        {
            await resultEventPublisher.PublishAsync(transaction, cancellationToken);
            telemetry.RecordReconciliationProcessed(transaction.Status, "publish_recovery");
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            telemetry.RecordFailure(ex.GetType().Name, transaction.PaymentMethod);
            logger.LogWarning(
                ex,
                "Payment result publish recovery failed for transaction {PaymentTransactionId}.",
                transaction.Id);
        }
    }

    private async Task PublishNeedReviewIfRequiredAsync(
        PaymentTransaction transaction,
        IOrderPaymentResultEventPublisher resultEventPublisher,
        CancellationToken cancellationToken)
    {
        if (transaction.Status == PaymentTransactionStatus.NeedReview)
        {
            await resultEventPublisher.PublishAsync(transaction, cancellationToken);
            telemetry.RecordReconciliationNeedReview(transaction.PaymentMethod);
        }
    }

    private static bool ShouldExpirePendingTransaction(
        PaymentTransaction transaction,
        DateTime? expirePendingBefore)
    {
        return expirePendingBefore.HasValue
            && transaction.Status == PaymentTransactionStatus.Pending
            && transaction.CreatedAt < expirePendingBefore.Value;
    }

    internal static int GetNextDelaySeconds(
        ReconciliationOptions reconciliationOptions,
        int processedCount,
        int currentIdleDelaySeconds)
    {
        var intervalSeconds = Math.Max(1, reconciliationOptions.IntervalSeconds);
        if (!reconciliationOptions.Enabled || processedCount > 0)
        {
            return intervalSeconds;
        }

        var maxIdleBackoffSeconds = Math.Max(intervalSeconds, reconciliationOptions.MaxIdleBackoffSeconds);
        if (currentIdleDelaySeconds <= 0)
        {
            return intervalSeconds;
        }

        return Math.Min(currentIdleDelaySeconds * 2, maxIdleBackoffSeconds);
    }

    private static void SetPaymentTransactionTags(Activity? activity, PaymentTransaction transaction)
    {
        activity?.SetTag("order.id", transaction.OrderId);
        activity?.SetTag("payment.transaction_id", transaction.Id);
        activity?.SetTag("payment.idempotency_key", transaction.IdempotencyKey);
        activity?.SetTag("payment.status", transaction.Status.ToString());
        activity?.SetTag("payment.method", transaction.PaymentMethod);
        activity?.SetTag("payment.gateway.transaction_id", transaction.GatewayTransactionId);
    }
}
