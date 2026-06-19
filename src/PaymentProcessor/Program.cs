using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<PaymentDbContext>("paymentdb");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(PaymentProcessorTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics.AddMeter(PaymentProcessorTelemetry.MeterName));

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();
builder.Services.AddOptions<PaymentGatewayResilienceOptions>()
    .BindConfiguration(nameof(PaymentGatewayResilienceOptions))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PaymentGatewayResilienceOptions>, PaymentGatewayResilienceOptionsValidator>();
builder.Services.AddOptions<ReconciliationOptions>()
    .BindConfiguration(nameof(ReconciliationOptions));

builder.Services.AddMemoryCache();
builder.Services.AddMigration<PaymentDbContext>();
builder.Services.AddScoped<IPaymentTransactionService, PaymentTransactionService>();
builder.Services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<PaymentDbContext>>();
builder.Services.AddScoped<SimulatedBankGatewayClient>();
builder.Services.AddSingleton<BankGatewayResiliencePipelineProvider>();
builder.Services.AddScoped<IBankGatewayClient>(serviceProvider =>
    new ResilientBankGatewayClient(
        serviceProvider.GetRequiredService<SimulatedBankGatewayClient>(),
        serviceProvider.GetRequiredService<IOptionsMonitor<PaymentGatewayResilienceOptions>>(),
        serviceProvider.GetRequiredService<BankGatewayResiliencePipelineProvider>()));
builder.Services.AddScoped<IOrderPaymentResultEventPublisher, OrderPaymentResultEventPublisher>();
builder.Services.AddScoped<IPaymentWebhookHandler, PaymentWebhookHandler>();
builder.Services.AddSingleton<IWebhookSignatureVerifier, HmacSha256WebhookSignatureVerifier>();
builder.Services.AddTransient<OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
builder.Services.AddHostedService<ReconciliationWorker>();
builder.Services.AddHostedService<PaymentOperationalMetricsWorker>();
builder.Services.AddSingleton<ChaosState>();
builder.Services.AddSingleton<PaymentProcessorTelemetry>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapChaosEndpoints(app.Environment, app.Configuration);
app.MapPaymentRepairEndpoints(app.Environment, app.Configuration);
app.MapPaymentWebhookEndpoints(app.Environment);

await app.RunAsync();
