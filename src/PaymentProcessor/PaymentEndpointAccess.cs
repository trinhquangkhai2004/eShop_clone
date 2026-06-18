namespace eShop.PaymentProcessor;

public static class PaymentEndpointAccess
{
    public const string EnableSensitiveEndpointsKey = "PaymentManagementEndpoints:EnableSensitiveEndpoints";

    public static bool IsSensitiveEndpointEnabled(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        return environment.IsDevelopment()
            || configuration.GetValue<bool>(EnableSensitiveEndpointsKey);
    }
}
