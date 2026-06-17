namespace eShop.PaymentProcessor.Chaos;

public sealed class ChaosState
{
    private readonly Lock _lock = new();
    private ChaosOptions _options = new();

    public ChaosOptions Get()
    {
        lock (_lock)
        {
            return _options with { };
        }
    }

    public void Set(ChaosOptions options)
    {
        lock (_lock)
        {
            _options = options;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _options = new ChaosOptions();
        }
    }
}
