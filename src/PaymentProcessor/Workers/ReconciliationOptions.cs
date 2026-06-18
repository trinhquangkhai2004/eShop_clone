namespace eShop.PaymentProcessor.Workers;

public sealed class ReconciliationOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int StaleTransactionThresholdSeconds { get; set; } = 300;
    public int ExpirationThresholdSeconds { get; set; } = 86400;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int MaxIdleBackoffSeconds { get; set; } = 300;
    public int MetricsRefreshSeconds { get; set; } = 30;
}
