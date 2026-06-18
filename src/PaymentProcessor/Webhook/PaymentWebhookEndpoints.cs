using System.Text;
using System.Text.Json;

namespace eShop.PaymentProcessor.Webhook;

public static class PaymentWebhookEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapPaymentWebhookEndpoints(
        this IEndpointRouteBuilder routes,
        IHostEnvironment environment)
    {
        var group = routes.MapGroup("/webhooks/payment")
            .WithTags("Payment Webhooks");

        group.MapPost("/callback", async (
            HttpRequest request,
            IWebhookSignatureVerifier signatureVerifier,
            IPaymentWebhookHandler webhookHandler,
            CancellationToken cancellationToken) =>
        {
            var payload = await ReadPayloadAsync(request, cancellationToken);
            var verification = signatureVerifier.Verify(payload, request.Headers);
            if (!verification.IsValid)
            {
                return verification.IsReplay
                    ? Results.BadRequest(verification.FailureReason)
                    : Results.Unauthorized();
            }

            var callback = DeserializeCallback(payload);
            if (callback is null)
            {
                return Results.BadRequest("Invalid payment callback payload.");
            }

            return ToResult(await webhookHandler.HandleAsync(callback, cancellationToken));
        });

        if (environment.IsDevelopment())
        {
            group.MapPost("/simulate", async (
                BankCallbackRequest callback,
                IPaymentWebhookHandler webhookHandler,
                CancellationToken cancellationToken) =>
            {
                return ToResult(await webhookHandler.HandleAsync(callback, cancellationToken));
            });
        }

        return routes;
    }

    private static async Task<string> ReadPayloadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static BankCallbackRequest? DeserializeCallback(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<BankCallbackRequest>(payload, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult ToResult(PaymentWebhookResult result)
    {
        var response = new
        {
            result.Outcome,
            result.Message,
            result.PaymentTransactionId,
            PaymentStatus = result.PaymentStatus?.ToString()
        };

        return result.Outcome switch
        {
            PaymentWebhookOutcome.InvalidRequest => Results.BadRequest(response),
            PaymentWebhookOutcome.NotFound => Results.NotFound(response),
            _ => Results.Ok(response)
        };
    }
}
