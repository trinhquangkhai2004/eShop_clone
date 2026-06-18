#nullable enable

namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderPaymentSucceededIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public int? PaymentTransactionId { get; init; }
    public string? GatewayTransactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }

    public OrderPaymentSucceededIntegrationEvent(int orderId) => OrderId = orderId;
}
