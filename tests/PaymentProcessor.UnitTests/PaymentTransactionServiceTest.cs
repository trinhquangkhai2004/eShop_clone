namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class PaymentTransactionServiceTest
{
    [TestMethod]
    public async Task CreateOrGetAsync_ConcurrentInsertRace_ReturnsExistingTransactionAsIdempotencyHit()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = CreateDbContextOptions(connection);

        await using (var setupContext = new PaymentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
        }

        await using var dbContext = new ThrowAfterSavePaymentDbContext(
            options,
            idempotencyKeyToThrowAfterSaving: "order:88:payment");
        var service = new PaymentTransactionService(dbContext);

        var result = await service.CreateOrGetAsync(
            orderId: 88,
            userId: "user-88",
            amount: 88.88m,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:88:payment",
            cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.IsIdempotencyHit);
        Assert.AreEqual(88, result.Transaction.OrderId);
        Assert.AreEqual("order:88:payment", result.Transaction.IdempotencyKey);
    }

    [TestMethod]
    public async Task CreateOrGetAsync_SameOrderDifferentIdempotencyKey_DoesNotReturnExistingTransaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = CreateDbContextOptions(connection);

        await using (var setupContext = new PaymentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);
            setupContext.PaymentTransactions.Add(new PaymentTransaction(
                orderId: 99,
                userId: "user-99",
                amount: 99.99m,
                currency: "USD",
                paymentMethod: "Simulated",
                idempotencyKey: "order:99:payment"));
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var dbContext = new PaymentDbContext(options);
        var service = new PaymentTransactionService(dbContext);

        try
        {
            await service.CreateOrGetAsync(
                orderId: 99,
                userId: "user-99",
                amount: 99.99m,
                currency: "USD",
                paymentMethod: "Simulated",
                idempotencyKey: "order:99:payment:attempt-2",
                cancellationToken: CancellationToken.None);

            Assert.Fail("Expected a DbUpdateException for duplicate OrderId with a different idempotency key.");
        }
        catch (DbUpdateException)
        {
        }
    }

    [TestMethod]
    public async Task FindNeedReviewAsync_ReturnsNeedReviewTransactionsOrderedByUpdatedAt()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = CreateDbContextOptions(connection);

        await using (var setupContext = new PaymentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(CancellationToken.None);

            var succeeded = CreateTransaction(orderId: 100);
            succeeded.MarkSucceeded("gateway-100");

            var needReviewOldest = CreateTransaction(orderId: 101);
            needReviewOldest.MarkNeedReview();
            SetUpdatedAt(needReviewOldest, DateTime.UtcNow.AddMinutes(-10));

            var needReviewNewest = CreateTransaction(orderId: 102);
            needReviewNewest.MarkNeedReview();
            SetUpdatedAt(needReviewNewest, DateTime.UtcNow);

            setupContext.PaymentTransactions.AddRange(succeeded, needReviewNewest, needReviewOldest);
            await setupContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var dbContext = new PaymentDbContext(options);
        var service = new PaymentTransactionService(dbContext);

        var transactions = await service.FindNeedReviewAsync(10, CancellationToken.None);
        var needReviewCount = await service.CountByStatusAsync(PaymentTransactionStatus.NeedReview, CancellationToken.None);

        Assert.HasCount(2, transactions);
        Assert.AreEqual(101, transactions[0].OrderId);
        Assert.AreEqual(102, transactions[1].OrderId);
        Assert.AreEqual(2, needReviewCount);
    }

    [TestMethod]
    public async Task MarkSucceededAsync_WithOutbox_PersistsPaymentAndEventInSameTransaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = CreateDbContextOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);

        var integrationEventLogService = new IntegrationEventLogService<PaymentDbContext>(dbContext);
        var service = new PaymentTransactionService(dbContext, integrationEventLogService);
        var transactionResult = await service.CreateOrGetAsync(
            orderId: 120,
            userId: "user-120",
            amount: 120,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:120:payment",
            cancellationToken: CancellationToken.None);

        await service.MarkSucceededAsync(
            transactionResult.Transaction,
            "gateway-120",
            CancellationToken.None);

        var transaction = await dbContext.PaymentTransactions.SingleAsync(CancellationToken.None);
        var outboxEvent = await dbContext.Set<IntegrationEventLogEntry>().SingleAsync(CancellationToken.None);

        Assert.AreEqual(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.IsNotNull(transaction.OutboxTransactionId);
        Assert.AreEqual(transaction.OutboxTransactionId, outboxEvent.TransactionId);
        Assert.AreEqual(EventStateEnum.NotPublished, outboxEvent.State);
        Assert.Contains(nameof(OrderPaymentSucceededIntegrationEvent), outboxEvent.EventTypeName);
        Assert.Contains("\"OrderId\": 120", outboxEvent.Content);
    }

    [TestMethod]
    public async Task PublishAsync_WithOutbox_PublishesPendingEventAndMarksPublished()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(CancellationToken.None);
        var options = CreateDbContextOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);

        var integrationEventLogService = new IntegrationEventLogService<PaymentDbContext>(dbContext);
        var service = new PaymentTransactionService(dbContext, integrationEventLogService);
        var transactionResult = await service.CreateOrGetAsync(
            orderId: 121,
            userId: "user-121",
            amount: 121,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: "order:121:payment",
            cancellationToken: CancellationToken.None);
        await service.MarkSucceededAsync(
            transactionResult.Transaction,
            "gateway-121",
            CancellationToken.None);

        var eventBus = Substitute.For<IEventBus>();
        IntegrationEvent publishedEvent = null!;
        eventBus.PublishAsync(Arg.Do<IntegrationEvent>(integrationEvent => publishedEvent = integrationEvent))
            .Returns(Task.CompletedTask);
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        using var telemetry = new PaymentProcessorTelemetry();
        var publisher = new OrderPaymentResultEventPublisher(
            eventBus,
            service,
            new ChaosState(),
            environment,
            telemetry,
            Substitute.For<ILogger<OrderPaymentResultEventPublisher>>(),
            integrationEventLogService);

        await publisher.PublishAsync(transactionResult.Transaction, CancellationToken.None);

        var outboxEvent = await dbContext.Set<IntegrationEventLogEntry>().SingleAsync(CancellationToken.None);
        Assert.AreEqual(EventStateEnum.Published, outboxEvent.State);
        Assert.AreEqual(1, outboxEvent.TimesSent);
        Assert.IsTrue(transactionResult.Transaction.ResultEventPublished);

        var succeededEvent = publishedEvent as OrderPaymentSucceededIntegrationEvent;
        Assert.IsNotNull(succeededEvent);
        Assert.AreEqual(121, succeededEvent.OrderId);
        Assert.AreEqual("gateway-121", succeededEvent.GatewayTransactionId);
    }

    private static DbContextOptions<PaymentDbContext> CreateDbContextOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static PaymentTransaction CreateTransaction(int orderId)
    {
        return new PaymentTransaction(
            orderId,
            userId: $"user-{orderId}",
            amount: 100,
            currency: "USD",
            paymentMethod: "Simulated",
            idempotencyKey: $"order:{orderId}:payment");
    }

    private static void SetUpdatedAt(PaymentTransaction transaction, DateTime updatedAt)
    {
        var property = typeof(PaymentTransaction).GetProperty(nameof(PaymentTransaction.UpdatedAt));
        Assert.IsNotNull(property);
        property.SetValue(transaction, updatedAt);
    }

    private sealed class ThrowAfterSavePaymentDbContext(
        DbContextOptions<PaymentDbContext> options,
        string idempotencyKeyToThrowAfterSaving) : PaymentDbContext(options)
    {
        private bool _hasThrown;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var shouldThrow = !_hasThrown
                && ChangeTracker.Entries<PaymentTransaction>()
                    .Any(entry =>
                        entry.State == EntityState.Added
                        && entry.Entity.IdempotencyKey == idempotencyKeyToThrowAfterSaving);

            var result = await base.SaveChangesAsync(cancellationToken);

            if (shouldThrow)
            {
                _hasThrown = true;
                throw new DbUpdateException("Simulated unique constraint race.");
            }

            return result;
        }
    }
}
