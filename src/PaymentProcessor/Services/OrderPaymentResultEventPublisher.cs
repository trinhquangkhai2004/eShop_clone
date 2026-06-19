using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Services;

public sealed class OrderPaymentResultEventPublisher(
    IEventBus eventBus,
    IPaymentTransactionService paymentTransactionService,
    ChaosState chaosState,
    IHostEnvironment environment,
    PaymentProcessorTelemetry telemetry,
    ILogger<OrderPaymentResultEventPublisher> logger,
    IIntegrationEventLogService? integrationEventLogService = null) : IOrderPaymentResultEventPublisher
{
    public async Task PublishAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction.ResultEventPublished)
        {
            return;
        }

        var outboxEvents = await FindOutboxEventsAsync(transaction);
        if (outboxEvents.Count > 0)
        {
            foreach (var outboxEvent in outboxEvents)
            {
                await PublishIntegrationEventAsync(
                    transaction,
                    outboxEvent.IntegrationEvent,
                    outboxEvent.EventId,
                    cancellationToken);
            }

            return;
        }

        var integrationEvent = PaymentResultIntegrationEventFactory.Create(transaction);
        if (integrationEvent is not null)
        {
            await PublishIntegrationEventAsync(
                transaction,
                integrationEvent,
                outboxEventId: null,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<IntegrationEventLogEntry>> FindOutboxEventsAsync(PaymentTransaction transaction)
    {
        if (integrationEventLogService is null || transaction.OutboxTransactionId is not { } outboxTransactionId)
        {
            return [];
        }

        var outboxEvents = await integrationEventLogService.RetrieveEventLogsPendingToPublishAsync(outboxTransactionId);
        return outboxEvents.ToList();
    }

    private async Task PublishIntegrationEventAsync(
        PaymentTransaction transaction,
        IntegrationEvent integrationEvent,
        Guid? outboxEventId,
        CancellationToken cancellationToken)
    {
        var chaos = chaosState.Get();
        var chaosActive = chaos.Enabled && environment.IsDevelopment();

        try
        {
            if (outboxEventId.HasValue && integrationEventLogService is not null)
            {
                await integrationEventLogService.MarkEventAsInProgressAsync(outboxEventId.Value);
            }

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

            if (outboxEventId.HasValue && integrationEventLogService is not null)
            {
                await integrationEventLogService.MarkEventAsPublishedAsync(outboxEventId.Value);
            }

            await paymentTransactionService.MarkResultPublishedAsync(transaction, cancellationToken);

            telemetry.RecordResultEventPublished(integrationEvent.GetType().Name, transaction.Status);
            PaymentProcessorTrace.LogPaymentResultEventPublished(
                logger,
                integrationEvent.Id,
                integrationEvent.GetType().Name,
                transaction.OrderId,
                transaction.Id);
        }
        catch
        {
            if (outboxEventId.HasValue && integrationEventLogService is not null)
            {
                await integrationEventLogService.MarkEventAsFailedAsync(outboxEventId.Value);
            }

            throw;
        }
    }
}
