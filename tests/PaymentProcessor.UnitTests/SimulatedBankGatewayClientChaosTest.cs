using System.Diagnostics;

namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class SimulatedBankGatewayClientChaosTest
{
    [TestMethod]
    public async Task ChargeAsync_ForceGatewayFailureInDevelopment_ReturnsFailed()
    {
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            ForceGatewayFailure = true
        });
        var client = CreateClient(chaosState, paymentSucceeded: true);

        var result = await client.ChargeAsync(CreateTransaction(400), CancellationToken.None);

        Assert.AreEqual(BankGatewayPaymentStatus.Failed, result.Status);
        Assert.AreEqual("ChaosGatewayFailure", result.FailureReason);
    }

    [TestMethod]
    public async Task ChargeAsync_GatewayFailureRateOneInDevelopment_ReturnsFailed()
    {
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            GatewayFailureRate = 1
        });
        var client = CreateClient(chaosState, paymentSucceeded: true);

        var result = await client.ChargeAsync(CreateTransaction(401), CancellationToken.None);

        Assert.AreEqual(BankGatewayPaymentStatus.Failed, result.Status);
        Assert.AreEqual("ChaosGatewayFailureRate", result.FailureReason);
    }

    [TestMethod]
    public async Task ChargeAsync_ForceProcessingTimeoutInDevelopment_Throws()
    {
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            ForceProcessingTimeout = true
        });
        var client = CreateClient(chaosState, paymentSucceeded: true);

        await AssertThrowsAsync<TimeoutException>(
            () => client.ChargeAsync(CreateTransaction(402), CancellationToken.None));
    }

    [TestMethod]
    public async Task QueryStatusAsync_ForceProcessingTimeoutInDevelopment_DoesNotThrow()
    {
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            ForceProcessingTimeout = true
        });
        var client = CreateClient(chaosState, paymentSucceeded: true);

        var result = await client.QueryStatusAsync(CreateTransaction(403), CancellationToken.None);

        Assert.AreEqual(BankGatewayPaymentStatus.Succeeded, result.Status);
    }

    [TestMethod]
    public async Task ChargeAsync_GatewayDelayInDevelopment_AppliesDelay()
    {
        var chaosState = new ChaosState();
        chaosState.Set(new ChaosOptions
        {
            Enabled = true,
            GatewayDelayMs = 20
        });
        var client = CreateClient(chaosState, paymentSucceeded: true);

        var startedAt = Stopwatch.GetTimestamp();
        await client.ChargeAsync(CreateTransaction(404), CancellationToken.None);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        Assert.IsTrue(elapsed >= TimeSpan.FromMilliseconds(10));
    }

    private static SimulatedBankGatewayClient CreateClient(ChaosState chaosState, bool paymentSucceeded)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions
        {
            PaymentSucceeded = paymentSucceeded,
            DefaultCurrency = "USD",
            DefaultPaymentMethod = "Simulated",
            WebhookTimestampToleranceSeconds = 300
        });

        return new SimulatedBankGatewayClient(
            chaosState,
            environment,
            options,
            new PaymentProcessorTelemetry(),
            Substitute.For<ILogger<SimulatedBankGatewayClient>>());
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
