namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderPaymentExpiredIntegrationEvent(int OrderId) : IntegrationEvent
{
    public int? PaymentTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}
