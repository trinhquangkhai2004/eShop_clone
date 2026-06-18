#nullable enable

namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderPaymentExpiredIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public int? PaymentTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }

    public OrderPaymentExpiredIntegrationEvent(int orderId) => OrderId = orderId;
}
