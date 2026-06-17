using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public interface IOrderPaymentResultEventPublisher
{
    Task PublishAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
}
