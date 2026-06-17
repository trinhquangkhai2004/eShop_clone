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
    .BindConfiguration(nameof(PaymentOptions));
builder.Services.AddOptions<ReconciliationOptions>()
    .BindConfiguration(nameof(ReconciliationOptions));

builder.Services.AddMigration<PaymentDbContext>();
builder.Services.AddScoped<IPaymentTransactionService, PaymentTransactionService>();
builder.Services.AddScoped<IBankGatewayClient, SimulatedBankGatewayClient>();
builder.Services.AddScoped<IOrderPaymentResultEventPublisher, OrderPaymentResultEventPublisher>();
builder.Services.AddTransient<OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
builder.Services.AddHostedService<ReconciliationWorker>();
builder.Services.AddSingleton<ChaosState>();
builder.Services.AddSingleton<PaymentProcessorTelemetry>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapChaosEndpoints(app.Environment);
app.MapPaymentRepairEndpoints(app.Environment);

await app.RunAsync();
