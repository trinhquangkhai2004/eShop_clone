using eShop.PaymentProcessor.Domain;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace eShop.PaymentProcessor.Services;

public sealed class BankGatewayResiliencePipelineProvider(
    ILogger<BankGatewayResiliencePipelineProvider> logger)
{
    private readonly object _lock = new();
    private PipelineCacheEntry? _cache;

    public ResiliencePipeline<BankGatewayResult> GetPipeline(PaymentGatewayResilienceOptions options)
    {
        if (!options.Enabled)
        {
            return ResiliencePipeline<BankGatewayResult>.Empty;
        }

        var key = PipelineKey.From(options);
        var cached = _cache;
        if (cached is not null && cached.Key == key)
        {
            return cached.Pipeline;
        }

        lock (_lock)
        {
            cached = _cache;
            if (cached is not null && cached.Key == key)
            {
                return cached.Pipeline;
            }

            var pipeline = CreatePipeline(options);
            _cache = new PipelineCacheEntry(key, pipeline);
            return pipeline;
        }
    }

    internal ResiliencePipeline<BankGatewayResult> CreatePipeline(PaymentGatewayResilienceOptions options)
    {
        var shouldHandle = CreateShouldHandlePredicate();

        var builder = new ResiliencePipelineBuilder<BankGatewayResult>();

        if (options.RetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<BankGatewayResult>
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = options.RetryAttempts,
                Delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = arguments =>
                {
                    logger.LogWarning(
                        "Retrying payment gateway call after transient failure. Attempt={AttemptNumber}, DelayMs={RetryDelayMs}",
                        arguments.AttemptNumber + 1,
                        arguments.RetryDelay.TotalMilliseconds);

                    return default;
                }
            });
        }

        return builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<BankGatewayResult>
            {
                ShouldHandle = shouldHandle,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakDurationSeconds),
                OnOpened = arguments =>
                {
                    logger.LogError(
                        "Payment gateway circuit breaker opened for {BreakDurationSeconds}s.",
                        arguments.BreakDuration.TotalSeconds);

                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Payment gateway circuit breaker closed.");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("Payment gateway circuit breaker half-opened.");
                    return default;
                }
            })
            .Build();
    }

    private static PredicateBuilder<BankGatewayResult> CreateShouldHandlePredicate()
    {
        return new PredicateBuilder<BankGatewayResult>()
            .Handle<TimeoutException>()
            .Handle<HttpRequestException>()
            .Handle<IOException>()
            .HandleResult(result => result.Status == BankGatewayPaymentStatus.Unknown);
    }

    private sealed record PipelineCacheEntry(
        PipelineKey Key,
        ResiliencePipeline<BankGatewayResult> Pipeline);

    private readonly record struct PipelineKey(
        bool Enabled,
        int RetryAttempts,
        int RetryDelayMilliseconds,
        double CircuitBreakerFailureRatio,
        int CircuitBreakerMinimumThroughput,
        int CircuitBreakerSamplingDurationSeconds,
        int CircuitBreakerBreakDurationSeconds)
    {
        public static PipelineKey From(PaymentGatewayResilienceOptions options) =>
            new(
                options.Enabled,
                options.RetryAttempts,
                options.RetryDelayMilliseconds,
                options.CircuitBreakerFailureRatio,
                options.CircuitBreakerMinimumThroughput,
                options.CircuitBreakerSamplingDurationSeconds,
                options.CircuitBreakerBreakDurationSeconds);
    }
}
