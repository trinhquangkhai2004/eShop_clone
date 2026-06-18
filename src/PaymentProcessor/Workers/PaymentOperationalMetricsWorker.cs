using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Workers;

public sealed class PaymentOperationalMetricsWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ReconciliationOptions> options,
    PaymentProcessorTelemetry telemetry,
    ILogger<PaymentOperationalMetricsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Payment operational metrics refresh failed.");
            }

            var refreshSeconds = Math.Max(5, options.CurrentValue.MetricsRefreshSeconds);
            await Task.Delay(TimeSpan.FromSeconds(refreshSeconds), stoppingToken);
        }
    }

    private async Task RefreshMetricsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var paymentTransactionService = scope.ServiceProvider.GetRequiredService<IPaymentTransactionService>();

        var needReviewCount = await paymentTransactionService.CountByStatusAsync(
            PaymentTransactionStatus.NeedReview,
            cancellationToken);
        var pendingCount = await paymentTransactionService.CountByStatusAsync(
            PaymentTransactionStatus.Pending,
            cancellationToken);
        var processingCount = await paymentTransactionService.CountByStatusAsync(
            PaymentTransactionStatus.Processing,
            cancellationToken);

        telemetry.SetOperationalSnapshot(needReviewCount, pendingCount, processingCount);
    }
}
