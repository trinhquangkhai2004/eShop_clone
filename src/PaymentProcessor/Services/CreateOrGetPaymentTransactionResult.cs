using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public sealed record CreateOrGetPaymentTransactionResult(
    PaymentTransaction Transaction,
    bool IsIdempotencyHit);
