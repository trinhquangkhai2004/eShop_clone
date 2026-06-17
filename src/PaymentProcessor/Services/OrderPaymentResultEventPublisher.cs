using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public sealed class OrderPaymentResultEventPublisher(
    IEventBus eventBus,
    IPaymentTransactionService paymentTransactionService,
    ChaosState chaosState,
    IHostEnvironment environment,
    PaymentProcessorTelemetry telemetry,
    ILogger<OrderPaymentResultEventPublisher> logger) : IOrderPaymentResultEventPublisher
{
    public async Task PublishAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction.ResultEventPublished)
        {
            return;
        }

        var integrationEvent = CreateResultEvent(transaction);
        if (integrationEvent is null)
        {
            return;
        }

        var chaos = chaosState.Get();
        var chaosActive = chaos.Enabled && environment.IsDevelopment();

        if (chaosActive && chaos.ForcePublishFailure)
        {
            telemetry.RecordChaosInjection("publish_failure");
            PaymentProcessorTrace.LogChaosPublishFailure(
                logger,
                transaction.OrderId,
                transaction.Status.ToString());

            throw new InvalidOperationException("CHAOS: Simulated publish failure.");
        }

        await eventBus.PublishAsync(integrationEvent);
        await paymentTransactionService.MarkResultPublishedAsync(transaction, cancellationToken);

        telemetry.RecordResultEventPublished(integrationEvent.GetType().Name, transaction.Status);
        PaymentProcessorTrace.LogPaymentResultEventPublished(
            logger,
            integrationEvent.Id,
            integrationEvent.GetType().Name,
            transaction.OrderId,
            transaction.Id);
    }

    private static IntegrationEvent? CreateResultEvent(PaymentTransaction transaction)
    {
        return transaction.Status switch
        {
            PaymentTransactionStatus.Succeeded => new OrderPaymentSucceededIntegrationEvent(transaction.OrderId),
            PaymentTransactionStatus.Failed => new OrderPaymentFailedIntegrationEvent(transaction.OrderId),
            _ => null
        };
    }
}
