namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderStatusChangedToStockConfirmedIntegrationEvent(
    int OrderId,
    string? BuyerIdentityGuid = null,
    decimal? Amount = null,
    string? Currency = null) : IntegrationEvent;
