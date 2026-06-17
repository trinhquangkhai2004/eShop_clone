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
        while (!stoppingToken.IsCancellationRequested)
        {
            var reconciliationOptions = options.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Max(1, reconciliationOptions.IntervalSeconds));

            if (reconciliationOptions.Enabled)
            {
                try
                {
                    await ReconcileAsync(reconciliationOptions, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Payment reconciliation cycle failed.");
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ReconcileAsync(
        ReconciliationOptions reconciliationOptions,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var paymentTransactionService = scope.ServiceProvider.GetRequiredService<IPaymentTransactionService>();
        var bankGatewayClient = scope.ServiceProvider.GetRequiredService<IBankGatewayClient>();
        var resultEventPublisher = scope.ServiceProvider.GetRequiredService<IOrderPaymentResultEventPublisher>();

        var batchSize = Math.Max(1, reconciliationOptions.BatchSize);
        var staleBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, reconciliationOptions.StaleTransactionThresholdSeconds));
        var maxAttempts = Math.Max(1, reconciliationOptions.MaxAttempts);

        var staleTransactions = await paymentTransactionService.FindPendingForReconciliationAsync(
            staleBefore,
            batchSize,
            cancellationToken);

        foreach (var transaction in staleTransactions)
        {
            await ReconcileTransactionAsync(
                transaction,
                bankGatewayClient,
                paymentTransactionService,
                resultEventPublisher,
                maxAttempts,
                cancellationToken);
        }

        var unpublishedTransactions = await paymentTransactionService.FindUnpublishedTerminalResultsAsync(
            batchSize,
            cancellationToken);

        foreach (var transaction in unpublishedTransactions)
        {
            await PublishRecoveredResultAsync(transaction, resultEventPublisher, cancellationToken);
        }
    }

    private async Task ReconcileTransactionAsync(
        PaymentTransaction transaction,
        IBankGatewayClient bankGatewayClient,
        IPaymentTransactionService paymentTransactionService,
        IOrderPaymentResultEventPublisher resultEventPublisher,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        using var activity = telemetry.StartActivity("payment.reconciliation.query_bank_status");
        SetPaymentTransactionTags(activity, transaction);
        activity?.SetTag("payment.gateway.provider", bankGatewayClient.Provider);

        try
        {
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
                    await paymentTransactionService.MarkFailedAsync(transaction, cancellationToken);
                    await resultEventPublisher.PublishAsync(transaction, cancellationToken);
                    telemetry.RecordReconciliationProcessed(transaction.Status, "bank_status_failed");
                    break;

                default:
                    await paymentTransactionService.RecordReconciliationAttemptAsync(
                        transaction,
                        maxAttempts,
                        cancellationToken);
                    RecordNeedReviewIfRequired(transaction);
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
            RecordNeedReviewIfRequired(transaction);
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

    private void RecordNeedReviewIfRequired(PaymentTransaction transaction)
    {
        if (transaction.Status == PaymentTransactionStatus.NeedReview)
        {
            telemetry.RecordReconciliationNeedReview(transaction.PaymentMethod);
        }
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
