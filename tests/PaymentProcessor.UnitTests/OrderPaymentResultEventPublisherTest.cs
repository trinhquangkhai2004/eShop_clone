namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class OrderPaymentResultEventPublisherTest
{
    [TestMethod]
    public async Task PublishAsync_SucceededTransaction_PublishesEnrichedSucceededEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderPaymentResultEventPublisher>>();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 42,
            userId: "user-42",
            amount: 250.50m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:42:payment");
        transaction.MarkSucceeded("gateway-42");

        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(transaction, CancellationToken.None);

        var succeededEvent = publishedEvent as OrderPaymentSucceededIntegrationEvent;
        Assert.IsNotNull(succeededEvent);
        Assert.AreEqual(42, succeededEvent.OrderId);
        Assert.AreEqual(transaction.Id, succeededEvent.PaymentTransactionId);
        Assert.AreEqual("gateway-42", succeededEvent.GatewayTransactionId);
        Assert.AreEqual(250.50m, succeededEvent.Amount);
        Assert.AreEqual("USD", succeededEvent.Currency);
        await paymentTransactionService.Received(1)
            .MarkResultPublishedAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PublishAsync_FailedTransaction_PublishesEnrichedFailedEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderPaymentResultEventPublisher>>();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 43,
            userId: "user-43",
            amount: 99.95m,
            currency: "EUR",
            paymentMethod: "Simulated",
            idempotencyKey: "order:43:payment");
        transaction.MarkFailed("insufficient_funds");

        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(transaction, CancellationToken.None);

        var failedEvent = publishedEvent as OrderPaymentFailedIntegrationEvent;
        Assert.IsNotNull(failedEvent);
        Assert.AreEqual(43, failedEvent.OrderId);
        Assert.AreEqual(transaction.Id, failedEvent.PaymentTransactionId);
        Assert.AreEqual("insufficient_funds", failedEvent.FailureReason);
        Assert.AreEqual(99.95m, failedEvent.Amount);
        Assert.AreEqual("EUR", failedEvent.Currency);
        await paymentTransactionService.Received(1)
            .MarkResultPublishedAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task PublishAsync_ExpiredTransaction_PublishesEnrichedExpiredEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderPaymentResultEventPublisher>>();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 44,
            userId: "user-44",
            amount: 44.44m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:44:payment");
        transaction.MarkExpired();

        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(transaction, CancellationToken.None);

        var expiredEvent = publishedEvent as OrderPaymentExpiredIntegrationEvent;
        Assert.IsNotNull(expiredEvent);
        Assert.AreEqual(44, expiredEvent.OrderId);
        Assert.AreEqual(transaction.Id, expiredEvent.PaymentTransactionId);
        Assert.AreEqual(44.44m, expiredEvent.Amount);
        Assert.AreEqual("USD", expiredEvent.Currency);
    }

    [TestMethod]
    public async Task PublishAsync_NeedReviewTransaction_PublishesEnrichedNeedReviewEvent()
    {
        var eventBus = Substitute.For<IEventBus>();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderPaymentResultEventPublisher>>();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 45,
            userId: "user-45",
            amount: 45.45m,
            currency: "EUR",
            paymentMethod: "Simulated",
            idempotencyKey: "order:45:payment");
        transaction.IncrementReconciliationAttempt(maxAttempts: 1);

        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(transaction, CancellationToken.None);

        var needReviewEvent = publishedEvent as OrderPaymentNeedReviewIntegrationEvent;
        Assert.IsNotNull(needReviewEvent);
        Assert.AreEqual(45, needReviewEvent.OrderId);
        Assert.AreEqual(transaction.Id, needReviewEvent.PaymentTransactionId);
        Assert.AreEqual(45.45m, needReviewEvent.Amount);
        Assert.AreEqual("EUR", needReviewEvent.Currency);
        Assert.AreEqual(1, needReviewEvent.ReconciliationAttempts);
    }

    [TestMethod]
    public async Task PublishAsync_ForcePublishFailureInDevelopment_ThrowsBeforeMarkingPublished()
    {
        var eventBus = Substitute.For<IEventBus>();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            ForcePublishFailure = true
        });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderPaymentResultEventPublisher>>();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 46,
            userId: "user-46",
            amount: 46.46m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:46:payment");
        transaction.MarkSucceeded("gateway-46");

        await AssertThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(transaction, CancellationToken.None));

        await eventBus.DidNotReceive()
            .PublishAsync(Arg.Any<IntegrationEvent>());
        await paymentTransactionService.DidNotReceive()
            .MarkResultPublishedAsync(transaction, Arg.Any<CancellationToken>());
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
    }
}
