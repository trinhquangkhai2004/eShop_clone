#nullable enable

namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderPaymentFailedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public int? PaymentTransactionId { get; init; }
    public string? FailureReason { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }

    public OrderPaymentFailedIntegrationEvent(int orderId) => OrderId = orderId;
}
