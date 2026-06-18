using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Repair;

public static class PaymentRepairEndpoints
{
    public static IEndpointRouteBuilder MapPaymentRepairEndpoints(
        this IEndpointRouteBuilder routes,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!PaymentEndpointAccess.IsSensitiveEndpointEnabled(environment, configuration))
        {
            return routes;
        }

        var group = routes.MapGroup("/repair/payment")
            .WithTags("Payment Repair");

        group.MapGet("/need-review", async (
            int? maxCount,
            IPaymentTransactionService paymentTransactionService,
            CancellationToken cancellationToken) =>
        {
            var limit = Math.Clamp(maxCount ?? 50, 1, 200);
            var transactions = await paymentTransactionService.FindNeedReviewAsync(limit, cancellationToken);

            return Results.Ok(transactions.Select(PaymentRepairTransactionResponse.From));
        });

        group.MapPost("/{transactionId:int}/force-succeed", async (
            int transactionId,
            string? gatewayTransactionId,
            IPaymentTransactionService paymentTransactionService,
            IOrderPaymentResultEventPublisher resultEventPublisher,
            CancellationToken cancellationToken) =>
        {
            var transaction = await paymentTransactionService.FindByIdAsync(transactionId, cancellationToken);
            if (transaction is null)
            {
                return Results.NotFound(new
                {
                    transactionId,
                    Status = "Payment transaction not found"
                });
            }

            var resolvedGatewayTransactionId = !string.IsNullOrWhiteSpace(gatewayTransactionId)
                ? gatewayTransactionId
                : transaction.GatewayTransactionId ?? $"manual-{transaction.Id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

            await paymentTransactionService.MarkSucceededAsync(
                transaction,
                resolvedGatewayTransactionId,
                cancellationToken);
            await resultEventPublisher.PublishAsync(transaction, cancellationToken);

            return Results.Ok(PaymentRepairTransactionResponse.From(transaction));
        });

        group.MapPost("/{transactionId:int}/force-fail", async (
            int transactionId,
            string? failureReason,
            IPaymentTransactionService paymentTransactionService,
            IOrderPaymentResultEventPublisher resultEventPublisher,
            CancellationToken cancellationToken) =>
        {
            var transaction = await paymentTransactionService.FindByIdAsync(transactionId, cancellationToken);
            if (transaction is null)
            {
                return Results.NotFound(new
                {
                    transactionId,
                    Status = "Payment transaction not found"
                });
            }

            await paymentTransactionService.MarkFailedAsync(
                transaction,
                string.IsNullOrWhiteSpace(failureReason) ? "operator_forced_failure" : failureReason,
                cancellationToken);
            await resultEventPublisher.PublishAsync(transaction, cancellationToken);

            return Results.Ok(PaymentRepairTransactionResponse.From(transaction));
        });

        group.MapPost("/stock-confirmed", async (
            RepairStockConfirmedPaymentRequest request,
            OrderStatusChangedToStockConfirmedIntegrationEventHandler handler) =>
        {
            if (request.OrderId <= 0)
            {
                return Results.BadRequest("orderId must be greater than 0.");
            }

            if (request.Amount is <= 0)
            {
                return Results.BadRequest("amount must be greater than 0 when provided.");
            }

            var integrationEvent = new OrderStatusChangedToStockConfirmedIntegrationEvent(
                request.OrderId,
                request.BuyerIdentityGuid,
                request.Amount,
                request.Currency);

            await handler.Handle(integrationEvent);

            return Results.Accepted($"/repair/payment/stock-confirmed/{request.OrderId}", new
            {
                request.OrderId,
                Status = "Payment repair submitted"
            });
        });

        return routes;
    }
}

public sealed record PaymentRepairTransactionResponse(
    int Id,
    int OrderId,
    string? UserId,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    string PaymentMethod,
    string IdempotencyKey,
    string? GatewayTransactionId,
    string? FailureReason,
    int ReconciliationAttempts,
    DateTime? LastReconciledAt,
    bool ResultEventPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static PaymentRepairTransactionResponse From(PaymentTransaction transaction) =>
        new(
            transaction.Id,
            transaction.OrderId,
            transaction.UserId,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.PaymentMethod,
            transaction.IdempotencyKey,
            transaction.GatewayTransactionId,
            transaction.FailureReason,
            transaction.ReconciliationAttempts,
            transaction.LastReconciledAt,
            transaction.ResultEventPublished,
            transaction.CreatedAt,
            transaction.UpdatedAt);
}
