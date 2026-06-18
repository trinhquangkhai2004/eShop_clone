using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Webhook;

public sealed class PaymentWebhookHandler(
    IPaymentTransactionService paymentTransactionService,
    IOrderPaymentResultEventPublisher resultEventPublisher,
    ILogger<PaymentWebhookHandler> logger) : IPaymentWebhookHandler
{
    public async Task<PaymentWebhookResult> HandleAsync(
        BankCallbackRequest callback,
        CancellationToken cancellationToken = default)
    {
        var gatewayStatus = GatewayStatusMapper.Map(callback.Status);
        if (gatewayStatus is null)
        {
            return PaymentWebhookResult.Invalid("Unsupported payment callback status.");
        }

        if (callback.PaymentTransactionId is <= 0)
        {
            return PaymentWebhookResult.Invalid("paymentTransactionId must be greater than 0 when provided.");
        }

        if (callback.OrderId is <= 0)
        {
            return PaymentWebhookResult.Invalid("orderId must be greater than 0 when provided.");
        }

        var transaction = await FindTransactionAsync(callback, cancellationToken);
        if (transaction is null)
        {
            return PaymentWebhookResult.NotFound("No payment transaction matched the callback correlation identifiers.");
        }

        var validationResult = ValidateCallbackAgainstTransaction(callback, transaction);
        if (validationResult is not null)
        {
            return validationResult;
        }

        if (transaction.IsTerminal && transaction.ResultEventPublished)
        {
            return PaymentWebhookResult.AlreadyProcessed(transaction, "Payment callback was already processed.");
        }

        switch (gatewayStatus)
        {
            case BankGatewayPaymentStatus.Succeeded:
                return await HandleSucceededAsync(callback, transaction, cancellationToken);

            case BankGatewayPaymentStatus.Failed:
                return await HandleFailedAsync(callback, transaction, cancellationToken);

            case BankGatewayPaymentStatus.Pending:
            case BankGatewayPaymentStatus.Unknown:
                return PaymentWebhookResult.AcceptedNoChange(
                    transaction,
                    "Payment callback accepted; transaction remains pending reconciliation.");

            default:
                return PaymentWebhookResult.Invalid("Unsupported payment callback status.");
        }
    }

    private async Task<PaymentTransaction?> FindTransactionAsync(
        BankCallbackRequest callback,
        CancellationToken cancellationToken)
    {
        if (callback.PaymentTransactionId is { } paymentTransactionId)
        {
            var transaction = await paymentTransactionService.FindByIdAsync(paymentTransactionId, cancellationToken);
            if (transaction is not null)
            {
                return transaction;
            }
        }

        if (!string.IsNullOrWhiteSpace(callback.GatewayTransactionId))
        {
            var transaction = await paymentTransactionService.FindByGatewayTransactionIdAsync(
                callback.GatewayTransactionId,
                cancellationToken);
            if (transaction is not null)
            {
                return transaction;
            }
        }

        if (callback.OrderId is { } orderId)
        {
            return await paymentTransactionService.FindByOrderIdAsync(orderId, cancellationToken);
        }

        return null;
    }

    private static PaymentWebhookResult? ValidateCallbackAgainstTransaction(
        BankCallbackRequest callback,
        PaymentTransaction transaction)
    {
        if (callback.Amount.HasValue && callback.Amount.Value != transaction.Amount)
        {
            return PaymentWebhookResult.Invalid("Callback amount does not match the payment transaction.");
        }

        if (!string.IsNullOrWhiteSpace(callback.Currency)
            && !string.Equals(callback.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentWebhookResult.Invalid("Callback currency does not match the payment transaction.");
        }

        if (callback.OrderId.HasValue && callback.OrderId.Value != transaction.OrderId)
        {
            return PaymentWebhookResult.Invalid("Callback orderId does not match the payment transaction.");
        }

        return null;
    }

    private async Task<PaymentWebhookResult> HandleSucceededAsync(
        BankCallbackRequest callback,
        PaymentTransaction transaction,
        CancellationToken cancellationToken)
    {
        var gatewayTransactionId = callback.GatewayTransactionId ?? transaction.GatewayTransactionId;
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return PaymentWebhookResult.Invalid("gatewayTransactionId is required for succeeded callbacks.");
        }

        if (transaction.Status != PaymentTransactionStatus.Succeeded
            || !string.Equals(transaction.GatewayTransactionId, gatewayTransactionId, StringComparison.Ordinal))
        {
            await paymentTransactionService.MarkSucceededAsync(
                transaction,
                gatewayTransactionId,
                cancellationToken);
        }

        await resultEventPublisher.PublishAsync(transaction, cancellationToken);
        logger.LogInformation(
            "Processed succeeded payment callback for transaction {PaymentTransactionId}.",
            transaction.Id);

        return PaymentWebhookResult.Processed(transaction, "Payment callback processed as succeeded.");
    }

    private async Task<PaymentWebhookResult> HandleFailedAsync(
        BankCallbackRequest callback,
        PaymentTransaction transaction,
        CancellationToken cancellationToken)
    {
        var failureReason = string.IsNullOrWhiteSpace(callback.FailureReason)
            ? callback.Status
            : callback.FailureReason;

        if (transaction.Status != PaymentTransactionStatus.Failed
            || !string.Equals(transaction.FailureReason, failureReason, StringComparison.Ordinal))
        {
            await paymentTransactionService.MarkFailedAsync(
                transaction,
                failureReason,
                cancellationToken);
        }

        await resultEventPublisher.PublishAsync(transaction, cancellationToken);
        logger.LogInformation(
            "Processed failed payment callback for transaction {PaymentTransactionId}.",
            transaction.Id);

        return PaymentWebhookResult.Processed(transaction, "Payment callback processed as failed.");
    }
}
