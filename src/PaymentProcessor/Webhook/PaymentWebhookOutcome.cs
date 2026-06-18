namespace eShop.PaymentProcessor.Webhook;

public enum PaymentWebhookOutcome
{
    Processed = 0,
    AcceptedNoChange = 1,
    AlreadyProcessed = 2,
    InvalidRequest = 3,
    NotFound = 4
}
