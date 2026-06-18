namespace eShop.PaymentProcessor;

public sealed class PaymentGatewayResilienceOptions
{
    public bool Enabled { get; set; } = true;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 100;
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; set; } = 4;
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
}
