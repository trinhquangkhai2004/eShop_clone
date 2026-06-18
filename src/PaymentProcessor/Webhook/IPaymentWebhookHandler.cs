namespace eShop.PaymentProcessor.Webhook;

public interface IPaymentWebhookHandler
{
    Task<PaymentWebhookResult> HandleAsync(
        BankCallbackRequest callback,
        CancellationToken cancellationToken = default);
}
