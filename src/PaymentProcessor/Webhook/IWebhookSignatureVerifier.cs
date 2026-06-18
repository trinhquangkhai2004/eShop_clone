using Microsoft.AspNetCore.Http;

namespace eShop.PaymentProcessor.Webhook;

public interface IWebhookSignatureVerifier
{
    WebhookSignatureVerificationResult Verify(string payload, IHeaderDictionary headers);
}
