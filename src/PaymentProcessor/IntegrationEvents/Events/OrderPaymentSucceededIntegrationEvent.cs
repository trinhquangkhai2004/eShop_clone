namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderPaymentSucceededIntegrationEvent(int OrderId) : IntegrationEvent
{
    public int? PaymentTransactionId { get; init; }
    public string? GatewayTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}
