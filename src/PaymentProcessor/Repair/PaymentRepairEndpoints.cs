namespace eShop.PaymentProcessor.Repair;

public static class PaymentRepairEndpoints
{
    public static IEndpointRouteBuilder MapPaymentRepairEndpoints(
        this IEndpointRouteBuilder routes,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return routes;
        }

        var group = routes.MapGroup("/repair/payment")
            .WithTags("Payment Repair");

        group.MapPost("/stock-confirmed", async (
            RepairStockConfirmedPaymentRequest request,
            OrderStatusChangedToStockConfirmedIntegrationEventHandler handler) =>
        {
            if (request.OrderId <= 0)
            {
                return Results.BadRequest("orderId must be greater than 0.");
            }

            if (request.Amount is <= 0)
            {
                return Results.BadRequest("amount must be greater than 0 when provided.");
            }

            var integrationEvent = new OrderStatusChangedToStockConfirmedIntegrationEvent(
                request.OrderId,
                request.BuyerIdentityGuid,
                request.Amount,
                request.Currency);

            await handler.Handle(integrationEvent);

            return Results.Accepted($"/repair/payment/stock-confirmed/{request.OrderId}", new
            {
                request.OrderId,
                Status = "Payment repair submitted"
            });
        });

        return routes;
    }
}
