namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class PaymentWebhookHandlerTest
{
    [TestMethod]
    public async Task HandleAsync_SucceededCallback_MarksSucceededAndPublishes()
    {
        var transaction = CreateTransaction(orderId: 201);
        transaction.MarkProcessing();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var handler = new PaymentWebhookHandler(
            paymentTransactionService,
            resultEventPublisher,
            Substitute.For<ILogger<PaymentWebhookHandler>>());

        paymentTransactionService.FindByOrderIdAsync(201, Arg.Any<CancellationToken>())
            .Returns(transaction);
        paymentTransactionService.When(service => service.MarkSucceededAsync(
                transaction,
                "gateway-201",
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.MarkSucceeded("gateway-201"));

        var result = await handler.HandleAsync(new BankCallbackRequest
        {
            OrderId = 201,
            GatewayTransactionId = "gateway-201",
            Status = "succeeded",
            Amount = 201,
            Currency = "USD"
        }, CancellationToken.None);

        Assert.AreEqual(PaymentWebhookOutcome.Processed, result.Outcome);
        await paymentTransactionService.Received(1)
            .MarkSucceededAsync(transaction, "gateway-201", Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_FailedCallback_MarksFailedWithReasonAndPublishes()
    {
        var transaction = CreateTransaction(orderId: 202);
        transaction.MarkProcessing();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var handler = new PaymentWebhookHandler(
            paymentTransactionService,
            resultEventPublisher,
            Substitute.For<ILogger<PaymentWebhookHandler>>());

        paymentTransactionService.FindByOrderIdAsync(202, Arg.Any<CancellationToken>())
            .Returns(transaction);
        paymentTransactionService.When(service => service.MarkFailedAsync(
                transaction,
                "card_declined",
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.MarkFailed("card_declined"));

        var result = await handler.HandleAsync(new BankCallbackRequest
        {
            OrderId = 202,
            Status = "failed",
            Amount = 202,
            Currency = "USD",
            FailureReason = "card_declined"
        }, CancellationToken.None);

        Assert.AreEqual(PaymentWebhookOutcome.Processed, result.Outcome);
        await paymentTransactionService.Received(1)
            .MarkFailedAsync(transaction, "card_declined", Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_DuplicateTerminalPublishedCallback_ReturnsAlreadyProcessed()
    {
        var transaction = CreateTransaction(orderId: 203);
        transaction.MarkSucceeded("gateway-203");
        transaction.MarkResultPublished();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var handler = new PaymentWebhookHandler(
            paymentTransactionService,
            resultEventPublisher,
            Substitute.For<ILogger<PaymentWebhookHandler>>());

        paymentTransactionService.FindByOrderIdAsync(203, Arg.Any<CancellationToken>())
            .Returns(transaction);

        var result = await handler.HandleAsync(new BankCallbackRequest
        {
            OrderId = 203,
            GatewayTransactionId = "gateway-203",
            Status = "succeeded",
            Amount = 203,
            Currency = "USD"
        }, CancellationToken.None);

        Assert.AreEqual(PaymentWebhookOutcome.AlreadyProcessed, result.Outcome);
        await paymentTransactionService.DidNotReceive()
            .MarkSucceededAsync(Arg.Any<PaymentTransaction>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await resultEventPublisher.DidNotReceive()
            .PublishAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_AmountMismatch_ReturnsInvalid()
    {
        var transaction = CreateTransaction(orderId: 204);
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var handler = new PaymentWebhookHandler(
            paymentTransactionService,
            resultEventPublisher,
            Substitute.For<ILogger<PaymentWebhookHandler>>());

        paymentTransactionService.FindByOrderIdAsync(204, Arg.Any<CancellationToken>())
            .Returns(transaction);

        var result = await handler.HandleAsync(new BankCallbackRequest
        {
            OrderId = 204,
            GatewayTransactionId = "gateway-204",
            Status = "succeeded",
            Amount = 999,
            Currency = "USD"
        }, CancellationToken.None);

        Assert.AreEqual(PaymentWebhookOutcome.InvalidRequest, result.Outcome);
        await resultEventPublisher.DidNotReceive()
            .PublishAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_NoMatchingTransaction_ReturnsNotFound()
    {
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var handler = new PaymentWebhookHandler(
            paymentTransactionService,
            resultEventPublisher,
            Substitute.For<ILogger<PaymentWebhookHandler>>());

        var result = await handler.HandleAsync(new BankCallbackRequest
        {
            OrderId = 205,
            GatewayTransactionId = "gateway-205",
            Status = "succeeded"
        }, CancellationToken.None);

        Assert.AreEqual(PaymentWebhookOutcome.NotFound, result.Outcome);
    }

    private static PaymentTransaction CreateTransaction(int orderId)
    {
        return new PaymentTransaction(
            orderId,
            userId: $"user-{orderId}",
            amount: orderId,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: $"order:{orderId}:payment");
    }
}
