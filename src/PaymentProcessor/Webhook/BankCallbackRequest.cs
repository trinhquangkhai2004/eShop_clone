namespace eShop.PaymentProcessor.Webhook;

public sealed class BankCallbackRequest
{
    public int? PaymentTransactionId { get; set; }
    public int? OrderId { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? FailureReason { get; set; }
    public string? Provider { get; set; }
}
