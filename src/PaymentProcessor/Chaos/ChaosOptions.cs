namespace eShop.PaymentProcessor.Chaos;

public sealed record ChaosOptions
{
    public bool Enabled { get; init; }
    public int GatewayDelayMs { get; init; }
    public double GatewayFailureRate { get; init; }
    public bool ForceGatewayFailure { get; init; }
    public bool ForcePublishFailure { get; init; }
    public bool ForceProcessingTimeout { get; init; }
}
