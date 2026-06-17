using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public interface IPaymentTransactionService
{
    Task<CreateOrGetPaymentTransactionResult> CreateOrGetAsync(
        int orderId,
        string? userId,
        decimal amount,
        string currency,
        string paymentMethod,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(
        PaymentTransaction transaction,
        string gatewayTransactionId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    Task MarkNeedReviewAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    Task RecordReconciliationAttemptAsync(
        PaymentTransaction transaction,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    Task MarkResultPublishedAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransaction>> FindPendingForReconciliationAsync(
        DateTime staleBefore,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransaction>> FindUnpublishedTerminalResultsAsync(
        int maxCount,
        CancellationToken cancellationToken = default);
}
