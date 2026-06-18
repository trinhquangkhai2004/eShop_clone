namespace eShop.PaymentProcessor.Chaos;

public static class ChaosEndpoints
{
    public static IEndpointRouteBuilder MapChaosEndpoints(
        this IEndpointRouteBuilder routes,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!PaymentEndpointAccess.IsSensitiveEndpointEnabled(environment, configuration))
        {
            return routes;
        }

        var group = routes.MapGroup("/chaos/payment")
            .WithTags("Chaos");

        group.MapGet("/", (ChaosState state) => Results.Ok(state.Get()));

        group.MapPut("/", (
            ChaosOptions options,
            ChaosState state,
            ILogger<ChaosState> logger) =>
        {
            if (options.GatewayDelayMs < 0)
            {
                return Results.BadRequest("gatewayDelayMs must be greater than or equal to 0.");
            }

            if (options.GatewayFailureRate is < 0 or > 1)
            {
                return Results.BadRequest("gatewayFailureRate must be between 0.0 and 1.0.");
            }

            state.Set(options);

            logger.LogWarning(
                "CHAOS MODE UPDATED: Enabled={Enabled}, DelayMs={DelayMs}, FailureRate={FailureRate}, ForceGatewayFailure={ForceGatewayFailure}, ForcePublishFailure={ForcePublishFailure}, ForceProcessingTimeout={ForceProcessingTimeout}",
                options.Enabled,
                options.GatewayDelayMs,
                options.GatewayFailureRate,
                options.ForceGatewayFailure,
                options.ForcePublishFailure,
                options.ForceProcessingTimeout);

            return Results.Ok(options);
        });

        group.MapDelete("/", (ChaosState state, ILogger<ChaosState> logger) =>
        {
            state.Reset();
            logger.LogWarning("CHAOS MODE RESET to defaults.");
            return Results.NoContent();
        });

        return routes;
    }
}
