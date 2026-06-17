using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public interface IBankGatewayClient
{
    string Provider { get; }

    Task<BankGatewayResult> ChargeAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<BankGatewayResult> QueryStatusAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default);
}
