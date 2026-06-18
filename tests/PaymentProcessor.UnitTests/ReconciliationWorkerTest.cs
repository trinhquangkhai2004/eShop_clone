namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class ReconciliationWorkerTest
{
    [TestMethod]
    public async Task ReconcileAsync_StaleProcessingTransactionResolvedSucceeded_MarksSucceededAndPublishes()
    {
        var transaction = CreateTransaction(orderId: 101);
        transaction.MarkProcessing();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(new[] { transaction });
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        bankGatewayClient.QueryStatusAsync(transaction, Arg.Any<CancellationToken>())
            .Returns(BankGatewayResult.Succeeded("gateway-101"));
        paymentTransactionService.When(service => service.MarkSucceededAsync(
                transaction,
                "gateway-101",
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.MarkSucceeded("gateway-101"));

        await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 3, batchSize: 10));

        await bankGatewayClient.Received(1)
            .QueryStatusAsync(transaction, Arg.Any<CancellationToken>());
        await paymentTransactionService.Received(1)
            .MarkSucceededAsync(transaction, "gateway-101", Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
        await paymentTransactionService.DidNotReceive()
            .RecordReconciliationAttemptAsync(
                Arg.Any<PaymentTransaction>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReconcileAsync_StalePendingTransactionUnresolved_RecordsAttemptAndPromotesNeedReview()
    {
        var transaction = CreateTransaction(orderId: 102);
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(new[] { transaction });
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        bankGatewayClient.QueryStatusAsync(transaction, Arg.Any<CancellationToken>())
            .Returns(BankGatewayResult.Unknown("gateway_timeout"));
        paymentTransactionService.When(service => service.RecordReconciliationAttemptAsync(
                transaction,
                1,
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.IncrementReconciliationAttempt(maxAttempts: 1));

        await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 1, batchSize: 10));

        await paymentTransactionService.Received(1)
            .RecordReconciliationAttemptAsync(transaction, 1, Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
        Assert.AreEqual(PaymentTransactionStatus.NeedReview, transaction.Status);
    }

    [TestMethod]
    public async Task ReconcileAsync_TerminalUnpublishedTransaction_PublishesRecoveredResult()
    {
        var transaction = CreateTransaction(orderId: 103);
        transaction.MarkFailed("card_declined");
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(new[] { transaction });

        await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 3, batchSize: 10));

        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
        await bankGatewayClient.DidNotReceive()
            .QueryStatusAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReconcileAsync_ExpiredPendingTransaction_MarksExpiredWithoutGatewayQuery()
    {
        var transaction = CreateTransaction(orderId: 105);
        SetCreatedAt(transaction, DateTime.UtcNow.AddDays(-2));
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(new[] { transaction });
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        paymentTransactionService.When(service => service.MarkExpiredAsync(
                transaction,
                Arg.Any<CancellationToken>()))
            .Do(_ => transaction.MarkExpired());

        await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 3, batchSize: 10, expirationThresholdSeconds: 86400));

        await paymentTransactionService.Received(1)
            .MarkExpiredAsync(transaction, Arg.Any<CancellationToken>());
        await bankGatewayClient.DidNotReceive()
            .QueryStatusAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
        Assert.AreEqual(PaymentTransactionStatus.Expired, transaction.Status);
    }

    [TestMethod]
    public async Task ReconcileAsync_StaleTransactionResolvedFailed_MarksFailedWithReasonAndPublishes()
    {
        var transaction = CreateTransaction(orderId: 104);
        transaction.MarkProcessing();
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(new[] { transaction });
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        bankGatewayClient.QueryStatusAsync(transaction, Arg.Any<CancellationToken>())
            .Returns(BankGatewayResult.Failed("insufficient_funds"));

        await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 3, batchSize: 10));

        await paymentTransactionService.Received(1)
            .MarkFailedAsync(transaction, "insufficient_funds", Arg.Any<CancellationToken>());
        await resultEventPublisher.Received(1)
            .PublishAsync(transaction, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReconcileAsync_NoTransactions_ReturnsZero()
    {
        var paymentTransactionService = Substitute.For<IPaymentTransactionService>();
        var bankGatewayClient = Substitute.For<IBankGatewayClient>();
        bankGatewayClient.Provider.Returns("Simulated");
        var resultEventPublisher = Substitute.For<IOrderPaymentResultEventPublisher>();
        using var telemetry = new PaymentProcessorTelemetry();
        var worker = CreateWorker(
            paymentTransactionService,
            bankGatewayClient,
            resultEventPublisher,
            telemetry);

        paymentTransactionService.FindPendingForReconciliationAsync(
                Arg.Any<DateTime>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());
        paymentTransactionService.FindUnpublishedTerminalResultsAsync(10, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PaymentTransaction>());

        var processedCount = await InvokeReconcileAsync(worker, CreateOptions(maxAttempts: 3, batchSize: 10));

        Assert.AreEqual(0, processedCount);
    }

    [TestMethod]
    public void GetNextDelaySeconds_IdleCycles_BackoffUpToMax()
    {
        var options = new ReconciliationOptions
        {
            Enabled = true,
            IntervalSeconds = 10,
            MaxIdleBackoffSeconds = 40
        };

        Assert.AreEqual(10, InvokeGetNextDelaySeconds(options, processedCount: 0, currentIdleDelaySeconds: 0));
        Assert.AreEqual(20, InvokeGetNextDelaySeconds(options, processedCount: 0, currentIdleDelaySeconds: 10));
        Assert.AreEqual(40, InvokeGetNextDelaySeconds(options, processedCount: 0, currentIdleDelaySeconds: 20));
        Assert.AreEqual(40, InvokeGetNextDelaySeconds(options, processedCount: 0, currentIdleDelaySeconds: 40));
        Assert.AreEqual(10, InvokeGetNextDelaySeconds(options, processedCount: 1, currentIdleDelaySeconds: 40));
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

    private static void SetCreatedAt(PaymentTransaction transaction, DateTime createdAt)
    {
        var property = typeof(PaymentTransaction).GetProperty(nameof(PaymentTransaction.CreatedAt));
        Assert.IsNotNull(property);
        property.SetValue(transaction, createdAt);
    }

    private static ReconciliationOptions CreateOptions(
        int maxAttempts,
        int batchSize,
        int expirationThresholdSeconds = 86400)
    {
        return new ReconciliationOptions
        {
            Enabled = true,
            IntervalSeconds = 60,
            StaleTransactionThresholdSeconds = 300,
            ExpirationThresholdSeconds = expirationThresholdSeconds,
            BatchSize = batchSize,
            MaxAttempts = maxAttempts
        };
    }

    private static ReconciliationWorker CreateWorker(
        IPaymentTransactionService paymentTransactionService,
        IBankGatewayClient bankGatewayClient,
        IOrderPaymentResultEventPublisher resultEventPublisher,
        PaymentProcessorTelemetry telemetry)
    {
        var services = new ServiceCollection()
            .AddSingleton(paymentTransactionService)
            .AddSingleton(bankGatewayClient)
            .AddSingleton(resultEventPublisher)
            .BuildServiceProvider();
        var options = Substitute.For<IOptionsMonitor<ReconciliationOptions>>();
        options.CurrentValue.Returns(new ReconciliationOptions());

        return new ReconciliationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            options,
            telemetry,
            Substitute.For<ILogger<ReconciliationWorker>>());
    }

    private static async Task<int> InvokeReconcileAsync(
        ReconciliationWorker worker,
        ReconciliationOptions options)
    {
        var method = typeof(ReconciliationWorker).GetMethod(
            "ReconcileAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<int>)method.Invoke(worker, new object[] { options, CancellationToken.None })!;
        Assert.IsNotNull(task);
        return await task;
    }

    private static int InvokeGetNextDelaySeconds(
        ReconciliationOptions options,
        int processedCount,
        int currentIdleDelaySeconds)
    {
        var method = typeof(ReconciliationWorker).GetMethod(
            "GetNextDelaySeconds",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        return (int)method.Invoke(null, new object[] { options, processedCount, currentIdleDelaySeconds })!;
    }
}
