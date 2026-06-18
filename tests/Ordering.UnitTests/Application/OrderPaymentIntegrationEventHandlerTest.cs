namespace eShop.Ordering.UnitTests.Application;

[TestClass]
public class OrderPaymentIntegrationEventHandlerTest
{
    [TestMethod]
    public async Task Handle_OrderPaymentExpired_SendsCancelOrderCommand()
    {
        var mediator = Substitute.For<IMediator>();
        var handler = new OrderPaymentExpiredIntegrationEventHandler(
            mediator,
            Substitute.For<ILogger<OrderPaymentExpiredIntegrationEventHandler>>());

        await handler.Handle(new OrderPaymentExpiredIntegrationEvent(orderId: 77)
        {
            PaymentTransactionId = 7001,
            Amount = 100,
            Currency = "USD"
        });

        await mediator.Received(1)
            .Send(
                Arg.Is<CancelOrderCommand>(command => command.OrderNumber == 77),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_OrderPaymentNeedReview_CompletesWithoutSendingCommand()
    {
        var handler = new OrderPaymentNeedReviewIntegrationEventHandler(
            Substitute.For<ILogger<OrderPaymentNeedReviewIntegrationEventHandler>>());

        await handler.Handle(new OrderPaymentNeedReviewIntegrationEvent(orderId: 78)
        {
            PaymentTransactionId = 7002,
            Amount = 101,
            Currency = "USD",
            ReconciliationAttempts = 5,
            FailureReason = "unknown_status"
        });
    }
}
