using System.Diagnostics;
using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IPaymentTransactionService paymentTransactionService,
    IBankGatewayClient bankGatewayClient,
    IOrderPaymentResultEventPublisher resultEventPublisher,
    PaymentProcessorTelemetry telemetry,
    ChaosState chaosState,
    IHostEnvironment environment,
    IOptionsMonitor<PaymentOptions> options,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var paymentOptions = options.CurrentValue;
        var paymentAmount = @event.Amount ?? paymentOptions.DefaultAmount;
        var paymentCurrency = string.IsNullOrWhiteSpace(@event.Currency)
            ? paymentOptions.DefaultCurrency
            : @event.Currency;
        var initialChaos = chaosState.Get();
        var initialChaosActive = initialChaos.Enabled && environment.IsDevelopment();
        var idempotencyKey = CreateIdempotencyKey(@event.OrderId);
        var paymentStatus = PaymentTransactionStatus.Pending;
        var idempotencyHit = false;

        using var processActivity = telemetry.StartActivity("payment.process");
        processActivity?.SetTag("order.id", @event.OrderId);
        processActivity?.SetTag("user.id", @event.BuyerIdentityGuid);
        processActivity?.SetTag("integration_event.id", @event.Id);
        processActivity?.SetTag("payment.idempotency_key", idempotencyKey);
        processActivity?.SetTag("payment.gateway.provider", bankGatewayClient.Provider);
        processActivity?.SetTag("payment.amount", paymentAmount);
        processActivity?.SetTag("payment.currency", paymentCurrency);
        processActivity?.SetTag("payment.method", paymentOptions.DefaultPaymentMethod);
        processActivity?.SetTag("chaos.enabled", initialChaosActive);

        try
        {
            PaymentProcessorTrace.LogPaymentTransactionCreateOrGetStarted(logger, @event.OrderId, idempotencyKey);

            CreateOrGetPaymentTransactionResult transactionResult;
            using (var createOrGetActivity = telemetry.StartActivity("payment.create_or_get_transaction"))
            {
                createOrGetActivity?.SetTag("order.id", @event.OrderId);
                createOrGetActivity?.SetTag("payment.idempotency_key", idempotencyKey);

                transactionResult = await paymentTransactionService.CreateOrGetAsync(
                    @event.OrderId,
                    @event.BuyerIdentityGuid,
                    paymentAmount,
                    paymentCurrency,
                    paymentOptions.DefaultPaymentMethod,
                    idempotencyKey);

                SetPaymentTransactionTags(createOrGetActivity, transactionResult.Transaction);
                createOrGetActivity?.SetTag("payment.idempotency.hit", transactionResult.IsIdempotencyHit);
            }

            var transaction = transactionResult.Transaction;
            idempotencyHit = transactionResult.IsIdempotencyHit;
            paymentStatus = transaction.Status;

            SetPaymentTransactionTags(processActivity, transaction);
            processActivity?.SetTag("payment.idempotency.hit", idempotencyHit);

            using var logScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["OrderId"] = transaction.OrderId,
                ["PaymentTransactionId"] = transaction.Id,
                ["IdempotencyKey"] = transaction.IdempotencyKey,
                ["PaymentStatus"] = transaction.Status,
                ["GatewayProvider"] = bankGatewayClient.Provider
            });

            if (idempotencyHit)
            {
                PaymentProcessorTrace.LogPaymentTransactionIdempotencyHit(
                    logger,
                    transaction.OrderId,
                    transaction.Id,
                    transaction.Status);

                telemetry.RecordIdempotencyHit(transaction.Status, transaction.PaymentMethod);

                if (transaction.IsTerminal && !transaction.ResultEventPublished)
                {
                    await resultEventPublisher.PublishAsync(transaction);
                }
                else if (!transaction.IsTerminal)
                {
                    await TryResolveNonTerminalIdempotencyHitAsync(
                        transaction,
                        bankGatewayClient,
                        resultEventPublisher);

                    paymentStatus = transaction.Status;
                }

                telemetry.RecordTransaction(transaction.Status, transaction.PaymentMethod);
                processActivity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            PaymentProcessorTrace.LogPaymentTransactionCreated(logger, transaction.OrderId, transaction.Id);

            await paymentTransactionService.MarkProcessingAsync(transaction);
            paymentStatus = transaction.Status;

            PaymentProcessorTrace.LogPaymentTransactionProcessingStarted(logger, transaction.OrderId, transaction.Id);

            using (var gatewayActivity = telemetry.StartActivity("payment.gateway.charge"))
            {
                SetPaymentTransactionTags(gatewayActivity, transaction);
                gatewayActivity?.SetTag("payment.gateway.provider", bankGatewayClient.Provider);

                PaymentProcessorTrace.LogPaymentGatewayChargeStarted(logger, transaction.OrderId, transaction.Id);

                var gatewayResult = await bankGatewayClient.ChargeAsync(transaction);
                gatewayActivity?.SetTag("payment.gateway.status", gatewayResult.Status.ToString());

                if (gatewayResult.Status == BankGatewayPaymentStatus.Succeeded
                    && !string.IsNullOrWhiteSpace(gatewayResult.GatewayTransactionId))
                {
                    await paymentTransactionService.MarkSucceededAsync(transaction, gatewayResult.GatewayTransactionId);
                    paymentStatus = transaction.Status;

                    gatewayActivity?.SetTag("payment.gateway.transaction_id", gatewayResult.GatewayTransactionId);
                    gatewayActivity?.SetTag("payment.status", transaction.Status.ToString());
                    gatewayActivity?.SetStatus(ActivityStatusCode.Ok);

                    PaymentProcessorTrace.LogPaymentGatewayChargeSucceeded(
                        logger,
                        transaction.OrderId,
                        transaction.Id,
                        gatewayResult.GatewayTransactionId);
                }
                else
                {
                    var failureReason = gatewayResult.FailureReason ?? gatewayResult.Status.ToString();
                    await paymentTransactionService.MarkFailedAsync(transaction, failureReason);
                    paymentStatus = transaction.Status;

                    gatewayActivity?.SetTag("payment.status", transaction.Status.ToString());
                    gatewayActivity?.SetTag("payment.failure_reason", failureReason);
                    gatewayActivity?.SetStatus(ActivityStatusCode.Error, failureReason);

                    telemetry.RecordFailure(failureReason, transaction.PaymentMethod);
                    PaymentProcessorTrace.LogPaymentGatewayChargeFailed(
                        logger,
                        transaction.OrderId,
                        transaction.Id,
                        failureReason);
                }
            }

            using (var publishActivity = telemetry.StartActivity("payment.publish_result_event"))
            {
                SetPaymentTransactionTags(publishActivity, transaction);

                await resultEventPublisher.PublishAsync(transaction);
            }

            telemetry.RecordTransaction(transaction.Status, transaction.PaymentMethod);
            processActivity?.SetTag("payment.status", transaction.Status.ToString());
            processActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            processActivity?.SetExceptionTags(ex);
            telemetry.RecordFailure(ex.GetType().Name, paymentOptions.DefaultPaymentMethod);
            throw;
        }
        finally
        {
            telemetry.RecordProcessingDuration(
                Stopwatch.GetElapsedTime(startedAt),
                paymentStatus,
                idempotencyHit);
        }
    }

    private static void SetPaymentTransactionTags(Activity? activity, PaymentTransaction transaction)
    {
        activity?.SetTag("order.id", transaction.OrderId);
        activity?.SetTag("user.id", transaction.UserId);
        activity?.SetTag("payment.transaction_id", transaction.Id);
        activity?.SetTag("payment.idempotency_key", transaction.IdempotencyKey);
        activity?.SetTag("payment.status", transaction.Status.ToString());
        activity?.SetTag("payment.method", transaction.PaymentMethod);
        activity?.SetTag("payment.amount", transaction.Amount);
        activity?.SetTag("payment.currency", transaction.Currency);
        activity?.SetTag("payment.gateway.transaction_id", transaction.GatewayTransactionId);
    }

    private async Task TryResolveNonTerminalIdempotencyHitAsync(
        PaymentTransaction transaction,
        IBankGatewayClient bankGatewayClient,
        IOrderPaymentResultEventPublisher resultEventPublisher)
    {
        using var reconciliationActivity = telemetry.StartActivity("payment.idempotency.reconcile");
        SetPaymentTransactionTags(reconciliationActivity, transaction);
        reconciliationActivity?.SetTag("payment.gateway.provider", bankGatewayClient.Provider);

        try
        {
            var gatewayResult = await bankGatewayClient.QueryStatusAsync(transaction);
            reconciliationActivity?.SetTag("payment.gateway.status", gatewayResult.Status.ToString());

            switch (gatewayResult.Status)
            {
                case BankGatewayPaymentStatus.Succeeded when !string.IsNullOrWhiteSpace(gatewayResult.GatewayTransactionId):
                    await paymentTransactionService.MarkSucceededAsync(
                        transaction,
                        gatewayResult.GatewayTransactionId);
                    await resultEventPublisher.PublishAsync(transaction);
                    break;

                case BankGatewayPaymentStatus.Failed:
                    await paymentTransactionService.MarkFailedAsync(
                        transaction,
                        gatewayResult.FailureReason ?? gatewayResult.Status.ToString());
                    await resultEventPublisher.PublishAsync(transaction);
                    break;
            }
        }
        catch (Exception ex)
        {
            reconciliationActivity?.SetExceptionTags(ex);
            telemetry.RecordFailure(ex.GetType().Name, transaction.PaymentMethod);
            logger.LogWarning(
                ex,
                "Unable to reconcile idempotency hit for payment transaction {PaymentTransactionId}. Background reconciliation will retry.",
                transaction.Id);
        }
    }

    private static string CreateIdempotencyKey(int orderId) => $"order:{orderId}:payment";
}
