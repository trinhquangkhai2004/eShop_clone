using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public sealed class SimulatedBankGatewayClient(
    ChaosState chaosState,
    IHostEnvironment environment,
    IOptionsMonitor<PaymentOptions> options,
    PaymentProcessorTelemetry telemetry,
    ILogger<SimulatedBankGatewayClient> logger) : IBankGatewayClient
{
    public string Provider => "Simulated";

    public async Task<BankGatewayResult> ChargeAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await ApplyGatewayChaosAsync(transaction.OrderId, allowProcessingTimeout: true, cancellationToken);

        var paymentOptions = options.CurrentValue;
        var chaos = chaosState.Get();
        var chaosActive = chaos.Enabled && environment.IsDevelopment();

        if (chaosActive && chaos.ForceGatewayFailure)
        {
            telemetry.RecordChaosInjection("gateway_failure");
            PaymentProcessorTrace.LogChaosGatewayFailure(logger, transaction.OrderId);
            return BankGatewayResult.Failed("ChaosGatewayFailure");
        }

        if (chaosActive
            && chaos.GatewayFailureRate > 0
            && Random.Shared.NextDouble() < chaos.GatewayFailureRate)
        {
            telemetry.RecordChaosInjection("gateway_failure_rate");
            PaymentProcessorTrace.LogChaosGatewayFailure(logger, transaction.OrderId);
            return BankGatewayResult.Failed("ChaosGatewayFailureRate");
        }

        return paymentOptions.PaymentSucceeded
            ? BankGatewayResult.Succeeded(CreateGatewayTransactionId(transaction))
            : BankGatewayResult.Failed("SimulatedPaymentFailure");
    }

    public async Task<BankGatewayResult> QueryStatusAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await ApplyGatewayChaosAsync(transaction.OrderId, allowProcessingTimeout: false, cancellationToken);

        var paymentOptions = options.CurrentValue;
        var chaos = chaosState.Get();
        var chaosActive = chaos.Enabled && environment.IsDevelopment();

        if (chaosActive && chaos.ForceGatewayFailure)
        {
            return BankGatewayResult.Failed("ChaosGatewayFailure");
        }

        return paymentOptions.PaymentSucceeded
            ? BankGatewayResult.Succeeded(transaction.GatewayTransactionId ?? CreateGatewayTransactionId(transaction))
            : BankGatewayResult.Failed("SimulatedPaymentFailure");
    }

    private async Task ApplyGatewayChaosAsync(
        int orderId,
        bool allowProcessingTimeout,
        CancellationToken cancellationToken)
    {
        var chaos = chaosState.Get();
        var chaosActive = chaos.Enabled && environment.IsDevelopment();

        if (!chaosActive)
        {
            return;
        }

        if (chaos.GatewayDelayMs > 0)
        {
            telemetry.RecordChaosInjection("gateway_delay");
            PaymentProcessorTrace.LogChaosGatewayDelay(logger, chaos.GatewayDelayMs, orderId);

            await Task.Delay(chaos.GatewayDelayMs, cancellationToken);
        }

        if (allowProcessingTimeout && chaos.ForceProcessingTimeout)
        {
            telemetry.RecordChaosInjection("processing_timeout");
            PaymentProcessorTrace.LogChaosProcessingTimeout(logger, orderId);

            throw new TimeoutException("CHAOS: Simulated processing timeout.");
        }
    }

    private static string CreateGatewayTransactionId(PaymentTransaction transaction) =>
        $"sim-{transaction.Id}-{Guid.NewGuid():N}";
}
