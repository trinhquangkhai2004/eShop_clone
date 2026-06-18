# Payment Service Production Hardening

## GatewayWebhookSecret rotation

1. Generate a new random secret in the target secret store.
2. Deploy the bank/provider callback configuration with the new secret.
3. Deploy PaymentProcessor with `PaymentOptions__GatewayWebhookSecret` set to the same value.
4. Send a signed canary callback to `POST /webhook/payment/bank-callback`.
5. Verify the callback returns `200 OK` or `202 Accepted` and the payment transaction moves to the expected terminal state.
6. Remove the old secret from the provider and secret store after the canary window.

Current verifier supports one active secret. For zero-downtime dual-secret rotation, extend `HmacSha256WebhookSignatureVerifier` to accept current and previous secrets during the overlap window.

## Sensitive endpoint access

Chaos and repair endpoints are enabled when either condition is true:

- `ASPNETCORE_ENVIRONMENT=Development`
- `PaymentManagementEndpoints__EnableSensitiveEndpoints=true`

Use the override only in a controlled staging environment. Do not enable it in production.

## Chaos validation commands

Assuming PaymentProcessor is listening on `http://localhost:5226`:

```powershell
Invoke-RestMethod -Method Get -Uri http://localhost:5226/chaos/payment/
Invoke-RestMethod -Method Put -Uri http://localhost:5226/chaos/payment/ -ContentType 'application/json' -Body '{"enabled":true,"gatewayDelayMs":250,"gatewayFailureRate":0,"forceGatewayFailure":false,"forcePublishFailure":false,"forceProcessingTimeout":false}'
Invoke-RestMethod -Method Put -Uri http://localhost:5226/chaos/payment/ -ContentType 'application/json' -Body '{"enabled":true,"gatewayDelayMs":0,"gatewayFailureRate":0,"forceGatewayFailure":true,"forcePublishFailure":false,"forceProcessingTimeout":false}'
Invoke-RestMethod -Method Put -Uri http://localhost:5226/chaos/payment/ -ContentType 'application/json' -Body '{"enabled":true,"gatewayDelayMs":0,"gatewayFailureRate":0,"forceGatewayFailure":false,"forcePublishFailure":true,"forceProcessingTimeout":false}'
Invoke-RestMethod -Method Put -Uri http://localhost:5226/chaos/payment/ -ContentType 'application/json' -Body '{"enabled":true,"gatewayDelayMs":0,"gatewayFailureRate":0,"forceGatewayFailure":false,"forcePublishFailure":false,"forceProcessingTimeout":true}'
Invoke-RestMethod -Method Delete -Uri http://localhost:5226/chaos/payment/
```

## E2E validation

Unit and integration-style tests cover order stock-confirmed event handling through PaymentProcessor persistence and result event publish. Full browser/service E2E still requires the Aspire stack and Docker:

```powershell
dotnet run --project src\eShop.AppHost\eShop.AppHost.csproj
```

Then place an order through the WebApp and verify:

- `payment."PaymentTransactions"` contains one `Succeeded` row for the order.
- Ordering consumes `OrderPaymentSucceededIntegrationEvent`.
- The order reaches the paid/confirmed path in Ordering.
- Grafana shows `payment_transactions_total{status="Succeeded"}` increasing.
