namespace eShop.PaymentProcessor;

public sealed class PaymentOptionsValidator : IValidateOptions<PaymentOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentOptions options)
    {
        var failures = new List<string>();

        if (options.DefaultAmount < 0)
        {
            failures.Add("PaymentOptions:DefaultAmount must be greater than or equal to 0.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultCurrency))
        {
            failures.Add("PaymentOptions:DefaultCurrency must not be empty.");
        }
        else if (options.DefaultCurrency.Length != 3)
        {
            failures.Add("PaymentOptions:DefaultCurrency must be a 3-letter ISO currency code.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultPaymentMethod))
        {
            failures.Add("PaymentOptions:DefaultPaymentMethod must not be empty.");
        }

        if (options.WebhookTimestampToleranceSeconds <= 0)
        {
            failures.Add("PaymentOptions:WebhookTimestampToleranceSeconds must be greater than 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
