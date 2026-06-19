namespace eShop.PaymentProcessor.Domain;

public class PaymentTransaction
{
    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public string? UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentTransactionStatus Status { get; private set; }
    public string PaymentMethod { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public int ReconciliationAttempts { get; private set; }
    public DateTime? LastReconciledAt { get; private set; }
    public Guid? OutboxTransactionId { get; private set; }
    public bool ResultEventPublished { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PaymentTransaction()
    {
        Currency = string.Empty;
        PaymentMethod = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public PaymentTransaction(
        int orderId,
        string? userId,
        decimal amount,
        string currency,
        string paymentMethod,
        string idempotencyKey)
    {
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        IdempotencyKey = idempotencyKey;
        Status = PaymentTransactionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public bool IsTerminal =>
        Status is PaymentTransactionStatus.Succeeded
            or PaymentTransactionStatus.Failed
            or PaymentTransactionStatus.Expired
            or PaymentTransactionStatus.Cancelled
            or PaymentTransactionStatus.NeedReview;

    public void MarkProcessing()
    {
        if (IsTerminal)
        {
            return;
        }

        Status = PaymentTransactionStatus.Processing;
        Touch();
    }

    public void MarkSucceeded(string gatewayTransactionId)
    {
        if (Status == PaymentTransactionStatus.Succeeded && ResultEventPublished)
        {
            return;
        }

        ResetResultEventPublishedWhenStatusChanges(PaymentTransactionStatus.Succeeded);
        GatewayTransactionId = gatewayTransactionId;
        FailureReason = null;
        Status = PaymentTransactionStatus.Succeeded;
        Touch();
    }

    public void MarkFailed(string? failureReason = null)
    {
        ResetResultEventPublishedWhenStatusChanges(PaymentTransactionStatus.Failed);
        FailureReason = failureReason;
        Status = PaymentTransactionStatus.Failed;
        Touch();
    }

    public void MarkExpired()
    {
        if (IsTerminal)
        {
            return;
        }

        Status = PaymentTransactionStatus.Expired;
        Touch();
    }

    public void MarkNeedReview()
    {
        ResetResultEventPublishedWhenStatusChanges(PaymentTransactionStatus.NeedReview);
        Status = PaymentTransactionStatus.NeedReview;
        Touch();
    }

    public void MarkResultPublished()
    {
        ResultEventPublished = true;
        Touch();
    }

    public void MarkOutboxTransaction(Guid transactionId)
    {
        OutboxTransactionId = transactionId;
    }

    public void MarkReconciled()
    {
        LastReconciledAt = DateTime.UtcNow;
    }

    public void IncrementReconciliationAttempt(int maxAttempts)
    {
        ReconciliationAttempts++;
        LastReconciledAt = DateTime.UtcNow;

        if (ReconciliationAttempts >= maxAttempts && !IsTerminal)
        {
            Status = PaymentTransactionStatus.NeedReview;
        }

        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    private void ResetResultEventPublishedWhenStatusChanges(PaymentTransactionStatus newStatus)
    {
        if (Status != newStatus)
        {
            ResultEventPublished = false;
        }
    }
}
