using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Webhook;

public sealed record PaymentWebhookResult(
    PaymentWebhookOutcome Outcome,
    string Message,
    int? PaymentTransactionId = null,
    PaymentTransactionStatus? PaymentStatus = null)
{
    public static PaymentWebhookResult Invalid(string message) =>
        new(PaymentWebhookOutcome.InvalidRequest, message);

    public static PaymentWebhookResult NotFound(string message) =>
        new(PaymentWebhookOutcome.NotFound, message);

    public static PaymentWebhookResult Processed(PaymentTransaction transaction, string message) =>
        new(PaymentWebhookOutcome.Processed, message, transaction.Id, transaction.Status);

    public static PaymentWebhookResult AcceptedNoChange(PaymentTransaction transaction, string message) =>
        new(PaymentWebhookOutcome.AcceptedNoChange, message, transaction.Id, transaction.Status);

    public static PaymentWebhookResult AlreadyProcessed(PaymentTransaction transaction, string message) =>
        new(PaymentWebhookOutcome.AlreadyProcessed, message, transaction.Id, transaction.Status);
}
