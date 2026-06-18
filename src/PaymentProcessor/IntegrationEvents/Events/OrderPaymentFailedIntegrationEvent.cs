namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderPaymentFailedIntegrationEvent(int OrderId) : IntegrationEvent
{
    public int? PaymentTransactionId { get; init; }
    public string? FailureReason { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}
