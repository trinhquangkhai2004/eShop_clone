namespace eShop.PaymentProcessor.Services;

public sealed record BankGatewayResult(
    BankGatewayPaymentStatus Status,
    string? GatewayTransactionId = null,
    string? FailureReason = null)
{
    public static BankGatewayResult Pending() => new(BankGatewayPaymentStatus.Pending);

    public static BankGatewayResult Succeeded(string gatewayTransactionId) =>
        new(BankGatewayPaymentStatus.Succeeded, gatewayTransactionId);

    public static BankGatewayResult Failed(string failureReason) =>
        new(BankGatewayPaymentStatus.Failed, FailureReason: failureReason);

    public static BankGatewayResult Unknown(string failureReason) =>
        new(BankGatewayPaymentStatus.Unknown, FailureReason: failureReason);
}
