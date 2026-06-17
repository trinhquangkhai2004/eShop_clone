namespace eShop.PaymentProcessor.Repair;

public sealed record RepairStockConfirmedPaymentRequest(
    int OrderId,
    string? BuyerIdentityGuid = null,
    decimal? Amount = null,
    string? Currency = null);
