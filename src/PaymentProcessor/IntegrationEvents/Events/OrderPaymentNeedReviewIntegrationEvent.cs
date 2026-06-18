namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderPaymentNeedReviewIntegrationEvent(int OrderId) : IntegrationEvent
{
    public int? PaymentTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public int? ReconciliationAttempts { get; init; }
    public string? FailureReason { get; init; }
}
