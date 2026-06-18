namespace eShop.PaymentProcessor;

public sealed class PaymentGatewayResilienceOptionsValidator : IValidateOptions<PaymentGatewayResilienceOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentGatewayResilienceOptions options)
    {
        var failures = new List<string>();

        if (options.RetryAttempts < 0)
        {
            failures.Add("PaymentGatewayResilienceOptions:RetryAttempts must be greater than or equal to 0.");
        }

        if (options.RetryDelayMilliseconds < 0)
        {
            failures.Add("PaymentGatewayResilienceOptions:RetryDelayMilliseconds must be greater than or equal to 0.");
        }

        if (options.CircuitBreakerFailureRatio is <= 0 or > 1)
        {
            failures.Add("PaymentGatewayResilienceOptions:CircuitBreakerFailureRatio must be greater than 0 and less than or equal to 1.");
        }

        if (options.CircuitBreakerMinimumThroughput <= 0)
        {
            failures.Add("PaymentGatewayResilienceOptions:CircuitBreakerMinimumThroughput must be greater than 0.");
        }

        if (options.CircuitBreakerSamplingDurationSeconds <= 0)
        {
            failures.Add("PaymentGatewayResilienceOptions:CircuitBreakerSamplingDurationSeconds must be greater than 0.");
        }

        if (options.CircuitBreakerBreakDurationSeconds <= 0)
        {
            failures.Add("PaymentGatewayResilienceOptions:CircuitBreakerBreakDurationSeconds must be greater than 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
