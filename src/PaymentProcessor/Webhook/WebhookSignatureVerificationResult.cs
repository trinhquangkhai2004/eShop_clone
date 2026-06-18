namespace eShop.PaymentProcessor.Webhook;

public sealed record WebhookSignatureVerificationResult(
    bool IsValid,
    bool IsReplay,
    string? FailureReason = null)
{
    public static WebhookSignatureVerificationResult Valid() => new(true, false);

    public static WebhookSignatureVerificationResult Invalid(string failureReason) =>
        new(false, false, failureReason);

    public static WebhookSignatureVerificationResult Replay(string failureReason) =>
        new(false, true, failureReason);
}
