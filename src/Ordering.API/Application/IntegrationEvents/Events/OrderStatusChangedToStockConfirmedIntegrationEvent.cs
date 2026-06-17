namespace eShop.Ordering.API.Application.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public OrderStatus OrderStatus { get; }
    public string BuyerName { get; }
    public string BuyerIdentityGuid { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    public OrderStatusChangedToStockConfirmedIntegrationEvent(
        int orderId,
        OrderStatus orderStatus,
        string buyerName,
        string buyerIdentityGuid,
        decimal amount,
        string currency = "USD")
    {
        OrderId = orderId;
        OrderStatus = orderStatus;
        BuyerName = buyerName;
        BuyerIdentityGuid = buyerIdentityGuid;
        Amount = amount;
        Currency = currency;
    }
}
