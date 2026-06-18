using eShop.PaymentProcessor.Domain;
using Polly;

namespace eShop.PaymentProcessor.Services;

public sealed class ResilientBankGatewayClient(
    IBankGatewayClient inner,
    IOptionsMonitor<PaymentGatewayResilienceOptions> options,
    BankGatewayResiliencePipelineProvider pipelineProvider) : IBankGatewayClient
{
    public string Provider => inner.Provider;

    public Task<BankGatewayResult> ChargeAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(token => inner.ChargeAsync(transaction, token), cancellationToken);
    }

    public Task<BankGatewayResult> QueryStatusAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(token => inner.QueryStatusAsync(transaction, token), cancellationToken);
    }

    private async Task<BankGatewayResult> ExecuteAsync(
        Func<CancellationToken, Task<BankGatewayResult>> gatewayCall,
        CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline(options.CurrentValue);

        return await pipeline.ExecuteAsync(
            token => new ValueTask<BankGatewayResult>(gatewayCall(token)),
            cancellationToken);
    }
}
