namespace eShop.Ordering.API.Application.IntegrationEvents.EventHandling;

public class OrderPaymentNeedReviewIntegrationEventHandler(
    ILogger<OrderPaymentNeedReviewIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderPaymentNeedReviewIntegrationEvent>
{
    public Task Handle(OrderPaymentNeedReviewIntegrationEvent @event)
    {
        logger.LogWarning(
            "Payment transaction {PaymentTransactionId} for order {OrderId} needs manual review after {ReconciliationAttempts} reconciliation attempts. Amount={Amount} {Currency}; FailureReason={FailureReason}",
            @event.PaymentTransactionId,
            @event.OrderId,
            @event.ReconciliationAttempts,
            @event.Amount,
            @event.Currency,
            @event.FailureReason);

        return Task.CompletedTask;
    }
}
