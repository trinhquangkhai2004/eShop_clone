namespace eShop.PaymentProcessor.Webhook;

public static class GatewayStatusMapper
{
    public static BankGatewayPaymentStatus? Map(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "success" or "succeeded" or "paid" or "captured" => BankGatewayPaymentStatus.Succeeded,
            "failed" or "failure" or "declined" or "cancelled" or "canceled" => BankGatewayPaymentStatus.Failed,
            "pending" or "processing" => BankGatewayPaymentStatus.Pending,
            "unknown" => BankGatewayPaymentStatus.Unknown,
            _ => null
        };
    }
}
