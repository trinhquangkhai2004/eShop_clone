namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class OrderStatusChangedToStockConfirmedIntegrationEventHandlerTest
{
    [TestMethod]
    public async Task Handle_IdempotencyHit_DoesNotPublishPaymentResultEvent()
    {
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        var options = CreateOptions(paymentSucceeded: true);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry,
            chaosState,
            environment,
            options,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 12,
            userId: "user-12",
            amount: 129.99m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:12:payment");
        transaction.MarkSucceeded("sim-existing");
        transaction.MarkResultPublished();

        paymentTransactionService.CreateOrGetAsync(
                12,
                "user-12",
                129.99m,
                "USD",
                "Simulated",
                "order:12:payment",
                Arg.Any<CancellationToken>())
            .Returns(new CreateOrGetPaymentTransactionResult(transaction, IsIdempotencyHit: true));

        await handler.Handle(new OrderStatusChangedToStockConfirmedIntegrationEvent(12, "user-12", 129.99m, "USD"));

        await resultEventPublisher.DidNotReceive()
            .PublishAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
        await paymentTransactionService.DidNotReceive()
            .MarkProcessingAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_NewSuccessfulPayment_PublishesSucceededEvent()
    {
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        var options = CreateOptions(paymentSucceeded: true);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry,
            chaosState,
            environment,
            options,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 12,
            userId: "user-12",
            amount: 129.99m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:12:payment");

        paymentTransactionService.CreateOrGetAsync(
                12,
                "user-12",
                129.99m,
                "USD",
                "Simulated",
                "order:12:payment",
                Arg.Any<CancellationToken>())
            .Returns(new CreateOrGetPaymentTransactionResult(transaction, IsIdempotencyHit: false));

        bankGatewayClient.ChargeAsync(transaction, Arg.Any<CancellationToken>())
            .Returns(BankGatewayResult.Succeeded("sim-12"));

        await handler.Handle(new OrderStatusChangedToStockConfirmedIntegrationEvent(12, "user-12", 129.99m, "USD"));

        await paymentTransactionService.Received(1)
            .MarkProcessingAsync(transaction, Arg.Any<CancellationToken>());
        await paymentTransactionService.Received(1)
            .MarkSucceededAsync(
                transaction,
                "sim-12",
                Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_IdempotencyHitProcessing_ReconcilesBankStatusAndPublishesSucceededEvent()
    {
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        var options = CreateOptions(paymentSucceeded: true);
        using var telemetry = new PaymentProcessorTelemetry();
        var logger = Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>();
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry,
            chaosState,
            environment,
            options,
            logger);

        var transaction = new PaymentTransaction(
            orderId: 12,
            userId: "user-12",
            amount: 129.99m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:12:payment");
        transaction.MarkProcessing();

        paymentTransactionService.CreateOrGetAsync(
                12,
                "user-12",
                129.99m,
                "USD",
                "Simulated",
                "order:12:payment",
                Arg.Any<CancellationToken>())
            .Returns(new CreateOrGetPaymentTransactionResult(transaction, IsIdempotencyHit: true));

        bankGatewayClient.QueryStatusAsync(transaction, Arg.Any<CancellationToken>())
            .Returns(BankGatewayResult.Succeeded("sim-reconciled"));

        paymentTransactionService.When(service => service.MarkSucceededAsync(
                transaction,
                "sim-reconciled",
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.MarkSucceeded("sim-reconciled"));

        await handler.Handle(new OrderStatusChangedToStockConfirmedIntegrationEvent(12, "user-12", 129.99m, "USD"));

        await bankGatewayClient.Received(1)
            .QueryStatusAsync(transaction, Arg.Any<CancellationToken>());
        await paymentTransactionService.Received(1)
            .MarkSucceededAsync(transaction, "sim-reconciled", Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
    }

    private static IOptionsMonitor<PaymentOptions> CreateOptions(bool paymentSucceeded)
    {
        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions
        {
            PaymentSucceeded = paymentSucceeded,
            DefaultAmount = 0,
            DefaultCurrency = "USD",
            DefaultPaymentMethod = "Simulated"
        });

        return options;
    }
}
