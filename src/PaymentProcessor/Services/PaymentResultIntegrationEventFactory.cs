using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public static class PaymentResultIntegrationEventFactory
{
    public static IntegrationEvent? Create(PaymentTransaction transaction)
    {
        return transaction.Status switch
        {
            PaymentTransactionStatus.Succeeded => new OrderPaymentSucceededIntegrationEvent(transaction.OrderId)
            {
                PaymentTransactionId = transaction.Id,
                GatewayTransactionId = transaction.GatewayTransactionId,
                Amount = transaction.Amount,
                Currency = transaction.Currency
            },
            PaymentTransactionStatus.Failed => new OrderPaymentFailedIntegrationEvent(transaction.OrderId)
            {
                PaymentTransactionId = transaction.Id,
                FailureReason = transaction.FailureReason,
                Amount = transaction.Amount,
                Currency = transaction.Currency
            },
            PaymentTransactionStatus.Expired => new OrderPaymentExpiredIntegrationEvent(transaction.OrderId)
            {
                PaymentTransactionId = transaction.Id,
                Amount = transaction.Amount,
                Currency = transaction.Currency
            },
            PaymentTransactionStatus.NeedReview => new OrderPaymentNeedReviewIntegrationEvent(transaction.OrderId)
            {
                PaymentTransactionId = transaction.Id,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                ReconciliationAttempts = transaction.ReconciliationAttempts,
                FailureReason = transaction.FailureReason
            },
            _ => null
        };
    }
}
