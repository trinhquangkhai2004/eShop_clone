namespace eShop.PaymentProcessor.UnitTests;

[TestClass]
public class HmacSha256WebhookSignatureVerifierTest
{
    [TestMethod]
    public void Verify_ValidSignature_ReturnsValid()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var verifier = new HmacSha256WebhookSignatureVerifier(CreateOptions(), cache);
        const string payload = """{"paymentTransactionId":12,"status":"succeeded"}""";
        var headers = CreateSignedHeaders(payload, nonce: "nonce-1");

        var result = verifier.Verify(payload, headers);

        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.IsReplay);
    }

    [TestMethod]
    public void Verify_InvalidSignature_ReturnsInvalid()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var verifier = new HmacSha256WebhookSignatureVerifier(CreateOptions(), cache);
        const string payload = """{"paymentTransactionId":12,"status":"succeeded"}""";
        var headers = CreateSignedHeaders(payload, nonce: "nonce-2");
        headers[HmacSha256WebhookSignatureVerifier.SignatureHeaderName] = "sha256=bad-signature";

        var result = verifier.Verify(payload, headers);

        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.IsReplay);
    }

    [TestMethod]
    public void Verify_ReusedNonce_ReturnsReplay()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var verifier = new HmacSha256WebhookSignatureVerifier(CreateOptions(), cache);
        const string payload = """{"paymentTransactionId":12,"status":"succeeded"}""";
        var headers = CreateSignedHeaders(payload, nonce: "nonce-3");

        var firstResult = verifier.Verify(payload, headers);
        var secondResult = verifier.Verify(payload, headers);

        Assert.IsTrue(firstResult.IsValid);
        Assert.IsFalse(secondResult.IsValid);
        Assert.IsTrue(secondResult.IsReplay);
    }

    private static HeaderDictionary CreateSignedHeaders(string payload, string nonce)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = HmacSha256WebhookSignatureVerifier.ComputeSignature(
            "test-webhook-secret",
            timestamp,
            payload);

        return new HeaderDictionary
        {
            [HmacSha256WebhookSignatureVerifier.SignatureHeaderName] = $"sha256={signature}",
            [HmacSha256WebhookSignatureVerifier.TimestampHeaderName] = timestamp.ToString(),
            [HmacSha256WebhookSignatureVerifier.NonceHeaderName] = nonce
        };
    }

    private static IOptionsMonitor<PaymentOptions> CreateOptions()
    {
        var options = Substitute.For<IOptionsMonitor<PaymentOptions>>();
        options.CurrentValue.Returns(new PaymentOptions
        {
            GatewayWebhookSecret = "test-webhook-secret",
            WebhookTimestampToleranceSeconds = 300
        });
        return options;
    }
}
