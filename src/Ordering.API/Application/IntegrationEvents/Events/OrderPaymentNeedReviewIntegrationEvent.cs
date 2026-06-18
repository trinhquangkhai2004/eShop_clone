#nullable enable

namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderPaymentNeedReviewIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public int? PaymentTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public int? ReconciliationAttempts { get; init; }
    public string? FailureReason { get; init; }

    public OrderPaymentNeedReviewIntegrationEvent(int orderId) => OrderId = orderId;
}
