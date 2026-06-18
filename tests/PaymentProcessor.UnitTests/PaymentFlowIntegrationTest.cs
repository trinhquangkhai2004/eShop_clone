namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class PaymentFlowIntegrationTest
{
    [TestMethod]
    public async Task Handle_OrderStockConfirmed_CreatesSucceededPaymentAndPublishesResultEvent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var dbContextOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new PaymentDbContext(dbContextOptions);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);

        var paymentTransactionService = new PaymentTransactionService(dbContext);
        var eventBus = Substitute.For<IEventBus>();
        var chaosState = new ChaosState();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var resultEventPublisher = new OrderPaymentResultEventPublisher(
            eventBus,
            paymentTransactionService,
            chaosState,
            environment,
            telemetry,
            Substitute.For<ILogger<OrderPaymentResultEventPublisher>>());
        var handler = new OrderStatusChangedToStockConfirmedIntegrationEventHandler(
            paymentTransactionService,
            new FixedBankGatewayClient(BankGatewayResult.Succeeded("gateway-500")),
            resultEventPublisher,
            telemetry,
            chaosState,
            environment,
            CreatePaymentOptions(),
            Substitute.For<ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler>>());

        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);

        await handler.Handle(new OrderStatusChangedToStockConfirmedIntegrationEvent(
            OrderId: 500,
            BuyerIdentityGuid: "user-500",
            Amount: 123.45m,
            Currency: "USD"));

        var transaction = await dbContext.PaymentTransactions.SingleAsync(CancellationToken.None);
        Assert.AreEqual(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.AreEqual("gateway-500", transaction.GatewayTransactionId);
        Assert.IsTrue(transaction.ResultEventPublished);

        var succeededEvent = publishedEvent as OrderPaymentSucceededIntegrationEvent;
        Assert.IsNotNull(succeededEvent);
        Assert.AreEqual(500, succeededEvent.OrderId);
        Assert.AreEqual(123.45m, succeededEvent.Amount);
        Assert.AreEqual("USD", succeededEvent.Currency);
    }

    private static IOptionsMonitor<PaymentOptions> CreatePaymentOptions()
    {
        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions
        {
            PaymentSucceeded = true,
            DefaultCurrency = "USD",
            DefaultPaymentMethod = "Simulated",
            WebhookTimestampToleranceSeconds = 300
        });

        return options;
    }

    private sealed class FixedBankGatewayClient(BankGatewayResult result) : IBankGatewayClient
    {
        public string Provider => "Fixed";

        public Task<BankGatewayResult> ChargeAsync(
            PaymentTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }

        public Task<BankGatewayResult> QueryStatusAsync(
            PaymentTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
