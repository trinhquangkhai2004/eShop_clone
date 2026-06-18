using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace eShop.PaymentProcessor.Webhook;

public sealed class HmacSha256WebhookSignatureVerifier(
    IOptionsMonitor<PaymentOptions> options,
    IMemoryCache replayCache) : IWebhookSignatureVerifier
{
    public const string SignatureHeaderName = "X-Payment-Signature";
    public const string TimestampHeaderName = "X-Payment-Timestamp";
    public const string NonceHeaderName = "X-Payment-Nonce";

    public WebhookSignatureVerificationResult Verify(string payload, IHeaderDictionary headers)
    {
        var paymentOptions = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(paymentOptions.GatewayWebhookSecret))
        {
            return WebhookSignatureVerificationResult.Invalid("Gateway webhook secret is not configured.");
        }

        if (!headers.TryGetValue(SignatureHeaderName, out var signatureValues)
            || string.IsNullOrWhiteSpace(signatureValues.ToString()))
        {
            return WebhookSignatureVerificationResult.Invalid("Missing webhook signature.");
        }

        if (!headers.TryGetValue(TimestampHeaderName, out var timestampValues)
            || !long.TryParse(timestampValues.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTimestamp))
        {
            return WebhookSignatureVerificationResult.Invalid("Missing or invalid webhook timestamp.");
        }

        if (!headers.TryGetValue(NonceHeaderName, out var nonceValues)
            || string.IsNullOrWhiteSpace(nonceValues.ToString()))
        {
            return WebhookSignatureVerificationResult.Invalid("Missing webhook nonce.");
        }

        var tolerance = TimeSpan.FromSeconds(Math.Max(1, paymentOptions.WebhookTimestampToleranceSeconds));
        var callbackTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        if ((DateTimeOffset.UtcNow - callbackTime).Duration() > tolerance)
        {
            return WebhookSignatureVerificationResult.Invalid("Webhook timestamp is outside the allowed tolerance.");
        }

        var nonce = nonceValues.ToString();
        var replayCacheKey = $"payment-webhook-nonce:{nonce}";
        if (replayCache.TryGetValue(replayCacheKey, out _))
        {
            return WebhookSignatureVerificationResult.Replay("Webhook nonce has already been used.");
        }

        var expectedSignature = ComputeSignature(
            paymentOptions.GatewayWebhookSecret,
            unixTimestamp,
            payload);
        var providedSignature = NormalizeSignature(signatureValues.ToString());
        if (!FixedTimeEquals(expectedSignature, providedSignature))
        {
            return WebhookSignatureVerificationResult.Invalid("Webhook signature is invalid.");
        }

        replayCache.Set(replayCacheKey, true, tolerance);
        return WebhookSignatureVerificationResult.Valid();
    }

    public static string ComputeSignature(string secret, long unixTimestamp, string payload)
    {
        var signedPayload = $"{unixTimestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexString(signatureBytes).ToLowerInvariant();
    }

    private static string NormalizeSignature(string signature)
    {
        const string Prefix = "sha256=";
        return signature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? signature[Prefix.Length..]
            : signature;
    }

    private static bool FixedTimeEquals(string expectedSignature, string providedSignature)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant());
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
