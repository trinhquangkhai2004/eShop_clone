using Polly.CircuitBreaker;

namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class BankGatewayResilienceTest
{
    [TestMethod]
    public async Task ChargeAsync_TransientTimeout_RetriesAndReturnsSuccess()
    {
        var inner = new SequenceBankGatewayClient(
            _ => throw new TimeoutException("temporary timeout"),
            _ => throw new TimeoutException("temporary timeout"),
            _ => Task.FromResult(BankGatewayResult.Succeeded("gateway-300")));
        var client = CreateClient(inner, new PaymentGatewayResilienceOptions
        {
            RetryAttempts = 3,
            RetryDelayMilliseconds = 1,
            CircuitBreakerMinimumThroughput = 100
        });

        var result = await client.ChargeAsync(CreateTransaction(300), CancellationToken.None);

        Assert.AreEqual(BankGatewayPaymentStatus.Succeeded, result.Status);
        Assert.AreEqual(3, inner.ChargeCallCount);
    }

    [TestMethod]
    public async Task ChargeAsync_RepeatedTransientFailures_OpensCircuitBreaker()
    {
        var inner = new SequenceBankGatewayClient(
            _ => throw new TimeoutException("gateway down"),
            _ => throw new TimeoutException("gateway still down"),
            _ => Task.FromResult(BankGatewayResult.Succeeded("should-not-run")));
        var client = CreateClient(inner, new PaymentGatewayResilienceOptions
        {
            RetryAttempts = 0,
            RetryDelayMilliseconds = 1,
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerMinimumThroughput = 2,
            CircuitBreakerSamplingDurationSeconds = 60,
            CircuitBreakerBreakDurationSeconds = 60
        });
        var transaction = CreateTransaction(301);

        await AssertThrowsAsync<TimeoutException>(
            () => client.ChargeAsync(transaction, CancellationToken.None));
        await AssertThrowsAsync<TimeoutException>(
            () => client.ChargeAsync(transaction, CancellationToken.None));
        await AssertThrowsAsync<BrokenCircuitException>(
            () => client.ChargeAsync(transaction, CancellationToken.None));

        Assert.AreEqual(2, inner.ChargeCallCount);
    }

    private static ResilientBankGatewayClient CreateClient(
        IBankGatewayClient inner,
        PaymentGatewayResilienceOptions resilienceOptions)
    {
        var options = Substitute.For<IOptionsMonitor<PaymentGatewayResilienceOptions>>();
        options.CurrentValue.Returns(resilienceOptions);

        return new ResilientBankGatewayClient(
            inner,
            options,
            new BankGatewayResiliencePipelineProvider(
                Substitute.For<ILogger<BankGatewayResiliencePipelineProvider>>()));
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

    private sealed class SequenceBankGatewayClient(params Func<PaymentTransaction, Task<BankGatewayResult>>[] chargeResults)
        : IBankGatewayClient
    {
        private int _chargeIndex;

        public int ChargeCallCount { get; private set; }

        public string Provider => "Sequence";

        public Task<BankGatewayResult> ChargeAsync(
            PaymentTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            ChargeCallCount++;
            var index = Math.Min(_chargeIndex++, chargeResults.Length - 1);
            return chargeResults[index](transaction);
        }

        public Task<BankGatewayResult> QueryStatusAsync(
            PaymentTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BankGatewayResult.Unknown("not_configured"));
        }
    }
}
