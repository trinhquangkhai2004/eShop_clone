using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Telemetry;

internal static partial class PaymentProcessorTrace
{
    [LoggerMessage(EventId = 1000, EventName = "PaymentTransactionCreateOrGetStarted", Level = LogLevel.Information, Message = "Creating or getting payment transaction for order {OrderId} with idempotency key {IdempotencyKey}")]
    public static partial void LogPaymentTransactionCreateOrGetStarted(ILogger logger, int orderId, string idempotencyKey);

    [LoggerMessage(EventId = 1001, EventName = "PaymentTransactionCreated", Level = LogLevel.Information, Message = "Payment transaction {PaymentTransactionId} created for order {OrderId}")]
    public static partial void LogPaymentTransactionCreated(ILogger logger, int orderId, int paymentTransactionId);

    [LoggerMessage(EventId = 1002, EventName = "PaymentTransactionIdempotencyHit", Level = LogLevel.Information, Message = "Payment transaction idempotency hit for order {OrderId}; existing transaction {PaymentTransactionId} has status {PaymentStatus}")]
    public static partial void LogPaymentTransactionIdempotencyHit(ILogger logger, int orderId, int paymentTransactionId, PaymentTransactionStatus paymentStatus);

    [LoggerMessage(EventId = 1003, EventName = "PaymentTransactionProcessingStarted", Level = LogLevel.Information, Message = "Payment transaction {PaymentTransactionId} processing started for order {OrderId}")]
    public static partial void LogPaymentTransactionProcessingStarted(ILogger logger, int orderId, int paymentTransactionId);

    [LoggerMessage(EventId = 1004, EventName = "PaymentGatewayChargeStarted", Level = LogLevel.Information, Message = "Payment gateway charge started for payment transaction {PaymentTransactionId} and order {OrderId}")]
    public static partial void LogPaymentGatewayChargeStarted(ILogger logger, int orderId, int paymentTransactionId);

    [LoggerMessage(EventId = 1005, EventName = "PaymentGatewayChargeSucceeded", Level = LogLevel.Information, Message = "Payment gateway charge succeeded for payment transaction {PaymentTransactionId}, order {OrderId}, gateway transaction {GatewayTransactionId}")]
    public static partial void LogPaymentGatewayChargeSucceeded(ILogger logger, int orderId, int paymentTransactionId, string gatewayTransactionId);

    [LoggerMessage(EventId = 1006, EventName = "PaymentGatewayChargeFailed", Level = LogLevel.Information, Message = "Payment gateway charge failed for payment transaction {PaymentTransactionId} and order {OrderId}. Reason: {FailureReason}")]
    public static partial void LogPaymentGatewayChargeFailed(ILogger logger, int orderId, int paymentTransactionId, string failureReason);

    [LoggerMessage(EventId = 1007, EventName = "PaymentResultEventPublished", Level = LogLevel.Information, Message = "Payment result integration event {IntegrationEventId} ({IntegrationEventName}) published for order {OrderId} and payment transaction {PaymentTransactionId}")]
    public static partial void LogPaymentResultEventPublished(ILogger logger, Guid integrationEventId, string integrationEventName, int orderId, int paymentTransactionId);

    [LoggerMessage(EventId = 2000, EventName = "ChaosGatewayDelay", Level = LogLevel.Warning, Message = "CHAOS: Injecting gateway delay {DelayMs}ms for order {OrderId}")]
    public static partial void LogChaosGatewayDelay(ILogger logger, int delayMs, int orderId);

    [LoggerMessage(EventId = 2001, EventName = "ChaosGatewayFailure", Level = LogLevel.Warning, Message = "CHAOS: Forcing gateway failure for order {OrderId}")]
    public static partial void LogChaosGatewayFailure(ILogger logger, int orderId);

    [LoggerMessage(EventId = 2002, EventName = "ChaosPublishFailure", Level = LogLevel.Warning, Message = "CHAOS: Forcing publish failure for order {OrderId}; DB status={DbStatus}, event will not be published")]
    public static partial void LogChaosPublishFailure(ILogger logger, int orderId, string dbStatus);

    [LoggerMessage(EventId = 2003, EventName = "ChaosProcessingTimeout", Level = LogLevel.Warning, Message = "CHAOS: Simulating processing timeout for order {OrderId}; transaction will remain Processing")]
    public static partial void LogChaosProcessingTimeout(ILogger logger, int orderId);
}
