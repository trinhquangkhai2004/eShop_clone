namespace eShop.Ordering.API.Application.IntegrationEvents.EventHandling;

public class OrderPaymentExpiredIntegrationEventHandler(
    IMediator mediator,
    ILogger<OrderPaymentExpiredIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderPaymentExpiredIntegrationEvent>
{
    public async Task Handle(OrderPaymentExpiredIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        var command = new CancelOrderCommand(@event.OrderId);

        logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command}) for expired payment transaction {PaymentTransactionId}",
            command.GetGenericTypeName(),
            nameof(command.OrderNumber),
            command.OrderNumber,
            command,
            @event.PaymentTransactionId);

        await mediator.Send(command);
    }
}
