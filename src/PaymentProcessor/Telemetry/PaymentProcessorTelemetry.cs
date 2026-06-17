using System.Diagnostics;
using System.Diagnostics.Metrics;
using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Telemetry;

public sealed class PaymentProcessorTelemetry : IDisposable
{
    public const string ActivitySourceName = "eShop.PaymentProcessor";
    public const string MeterName = "eShop.PaymentProcessor";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _paymentTransactionsTotal;
    private readonly Counter<long> _paymentIdempotencyHitsTotal;
    private readonly Counter<long> _paymentResultEventsPublishedTotal;
    private readonly Counter<long> _paymentFailuresTotal;
    private readonly Counter<long> _paymentReconciliationProcessedTotal;
    private readonly Counter<long> _paymentReconciliationNeedReviewTotal;
    private readonly Counter<long> _chaosInjectionsTotal;
    private readonly Histogram<double> _paymentProcessingDurationMs;

    public ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    public PaymentProcessorTelemetry()
    {
        _paymentTransactionsTotal = _meter.CreateCounter<long>(
            "payment_transactions_total",
            description: "Number of payment transactions processed by status.");

        _paymentIdempotencyHitsTotal = _meter.CreateCounter<long>(
            "payment_idempotency_hits_total",
            description: "Number of payment transactions skipped due to idempotency.");

        _paymentResultEventsPublishedTotal = _meter.CreateCounter<long>(
            "payment_result_events_published_total",
            description: "Number of payment result integration events published.");

        _paymentFailuresTotal = _meter.CreateCounter<long>(
            "payment_failures_total",
            description: "Number of failed payment transactions.");

        _paymentReconciliationProcessedTotal = _meter.CreateCounter<long>(
            "payment_reconciliation_processed_total",
            description: "Number of payment transactions processed by reconciliation.");

        _paymentReconciliationNeedReviewTotal = _meter.CreateCounter<long>(
            "payment_reconciliation_need_review_total",
            description: "Number of payment transactions moved to manual review by reconciliation.");

        _chaosInjectionsTotal = _meter.CreateCounter<long>(
            "payment_chaos_injections_total",
            description: "Number of chaos fault injections by type.");

        _paymentProcessingDurationMs = _meter.CreateHistogram<double>(
            "payment_processing_duration_ms",
            unit: "ms",
            description: "Payment processing duration in milliseconds.");
    }

    public Activity? StartActivity(string name) =>
        ActivitySource.StartActivity(name, ActivityKind.Internal);

    public void RecordTransaction(PaymentTransactionStatus status, string paymentMethod)
    {
        _paymentTransactionsTotal.Add(1,
            new KeyValuePair<string, object?>("status", status.ToString()),
            new KeyValuePair<string, object?>("payment_method", paymentMethod));
    }

    public void RecordIdempotencyHit(PaymentTransactionStatus status, string paymentMethod)
    {
        _paymentIdempotencyHitsTotal.Add(1,
            new KeyValuePair<string, object?>("status", status.ToString()),
            new KeyValuePair<string, object?>("payment_method", paymentMethod));
    }

    public void RecordResultEventPublished(string eventName, PaymentTransactionStatus status)
    {
        _paymentResultEventsPublishedTotal.Add(1,
            new KeyValuePair<string, object?>("event", eventName),
            new KeyValuePair<string, object?>("status", status.ToString()));
    }

    public void RecordFailure(string reason, string paymentMethod)
    {
        _paymentFailuresTotal.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("payment_method", paymentMethod));
    }

    public void RecordReconciliationProcessed(PaymentTransactionStatus status, string outcome)
    {
        _paymentReconciliationProcessedTotal.Add(1,
            new KeyValuePair<string, object?>("status", status.ToString()),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void RecordReconciliationNeedReview(string paymentMethod)
    {
        _paymentReconciliationNeedReviewTotal.Add(1,
            new KeyValuePair<string, object?>("payment_method", paymentMethod));
    }

    public void RecordChaosInjection(string type)
    {
        _chaosInjectionsTotal.Add(1,
            new KeyValuePair<string, object?>("type", type));
    }

    public void RecordProcessingDuration(TimeSpan duration, PaymentTransactionStatus status, bool idempotencyHit)
    {
        _paymentProcessingDurationMs.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("status", status.ToString()),
            new KeyValuePair<string, object?>("idempotency_hit", idempotencyHit));
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
