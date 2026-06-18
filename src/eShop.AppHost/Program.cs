using eShop.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var observabilityPath = FindObservabilityPath();
const string JaegerOtlpEndpoint = "http://localhost:4317";

var jaeger = builder
    .AddContainer("jaeger", "jaegertracing/all-in-one", "1.69.0")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true")
    .WithEndpoint(port: 16686, targetPort: 16686, name: "http")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc");

var prometheus = builder
    .AddContainer("prometheus", "prom/prometheus", "v3.5.0")
    .WithBindMount(Path.Combine(observabilityPath, "prometheus.yml"), "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithBindMount(Path.Combine(observabilityPath, "alerts"), "/etc/prometheus/alerts", isReadOnly: true)
    .WithArgs("--config.file=/etc/prometheus/prometheus.yml", "--storage.tsdb.retention.time=2h")
    .WithEndpoint(port: 9090, targetPort: 9090, name: "http");

builder.AddForwardedHeaders();

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("eventbus").WithLifetime(ContainerLifetime.Persistent);
var postgres = builder
    .AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume() // Lưu trữ dữ liệu cơ sở dữ liệu vĩnh viễn trên ổ đĩa cứng thông qua Docker Volume
    .WithPgAdmin(); // pgAdmin web UI để xem và quản lý database

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var orderDb = postgres.AddDatabase("orderingdb");
var webhooksDb = postgres.AddDatabase("webhooksdb");
var discountDb = postgres.AddDatabase("discountdb");
var paymentDb = postgres.AddDatabase("paymentdb");
var cosmos = builder.AddAzureCosmosDB("cosmos").RunAsEmulator();
var chatDb = cosmos.AddCosmosDatabase("chatdb");

// Copy dashboards into the provisioning folder directly to avoid Docker bind mount issues
var srcDashboards = Path.Combine(observabilityPath, "grafana", "dashboards");
var destDashboards = Path.Combine(observabilityPath, "grafana", "provisioning", "dashboards");
if (Directory.Exists(srcDashboards))
{
    Directory.CreateDirectory(destDashboards);
    foreach (var file in Directory.GetFiles(srcDashboards, "*.json"))
    {
        var content = File.ReadAllText(file);
        File.WriteAllText(Path.Combine(destDashboards, Path.GetFileName(file)), content, new System.Text.UTF8Encoding(false));
    }
}

var grafana = builder
    .AddContainer("grafana", "grafana/grafana", "12.1.0")
    .WithBindMount(Path.Combine(observabilityPath, "grafana", "provisioning"), "/etc/grafana/provisioning", isReadOnly: false)
    .WithBindMount(Path.Combine(observabilityPath, "grafana", "init-grafana.sh"), "/opt/eshop/init-grafana.sh", isReadOnly: true)
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", "sh /opt/eshop/init-grafana.sh && /run.sh")
    .WithReference(paymentDb)
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("GF_PANELS_DISABLE_SANITIZE_HTML", "true")
    .WithEndpoint(port: 3000, targetPort: 3000, name: "http")
    .WaitFor(prometheus)
    .WaitFor(jaeger)
    .WaitFor(paymentDb);

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

var discountApi = builder
    .AddProject<Projects.Discount_API>("discount-api")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(discountDb) //Connect DB
    .WithReference(redis) //Connect cache
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq);

// Services
var identityApi = builder
    .AddProject<Projects.Identity_API>("identity-api", launchProfileName)
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithExternalHttpEndpoints()
    .WithReference(identityDb)
    .WaitFor(jaeger)
    .WithHttpHealthCheck("/health");

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder
    .AddProject<Projects.Basket_API>("basket-api")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(redis)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithEnvironment("Identity__Url", identityEndpoint);
redis.WithParentRelationship(basketApi);

var catalogApi = builder
    .AddProject<Projects.Catalog_API>("catalog-api")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithReference(catalogDb)
    .WithReference(chatDb)
    .WithEnvironment("Identity__Url", identityEndpoint);

var orderingApi = builder
    .AddProject<Projects.Ordering_API>("ordering-api")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithReference(orderDb)
    .WaitFor(orderDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint)
    .WithReference(discountApi);

builder
    .AddProject<Projects.OrderProcessor>("order-processor")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithReference(orderDb)
    .WaitFor(orderingApi); // wait for the orderingApi to be ready because that contains the EF migrations

builder
    .AddProject<Projects.PaymentProcessor>("payment-processor")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithReference(paymentDb)
    .WaitFor(paymentDb);

var webHooksApi = builder
    .AddProject<Projects.Webhooks_API>("webhooks-api")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WithReference(webhooksDb)
    .WithEnvironment("Identity__Url", identityEndpoint);

// Reverse proxies
builder
    .AddYarp("mobile-bff")
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithExternalHttpEndpoints()
    .WaitFor(jaeger)
    .ConfigureMobileBffRoutes(catalogApi, orderingApi, identityApi);

// Apps
var webhooksClient = builder
    .AddProject<Projects.WebhookClient>("webhooksclient", launchProfileName)
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithReference(webHooksApi)
    .WaitFor(jaeger)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder
    .AddProject<Projects.WebApp>("webapp", launchProfileName)
    .WithEnvironment("ESHOP_OTEL_JAEGER_ENDPOINT", JaegerOtlpEndpoint)
    .WithExternalHttpEndpoints()
    .WithUrls(c =>
        c.Urls.ForEach(u => u.DisplayText = $"Online Store ({u.Endpoint?.EndpointName})")
    )
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(rabbitMq)
    .WaitFor(jaeger)
    .WaitFor(rabbitMq)
    .WaitFor(identityApi)
    .WithEnvironment("IdentityUrl", identityEndpoint)
    .WithReference(discountApi);

// set to true if you want to use OpenAI
bool useOpenAI = false;
if (useOpenAI)
{
    builder.AddOpenAI(catalogApi, webApp, OpenAITarget.OpenAI); // set to AzureOpenAI if you want to use Azure OpenAI
}

bool useOllama = false;
if (useOllama)
{
    builder.AddOllama(catalogApi, webApp);
}

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identityApi
    .WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
    .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
    .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
    .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
    .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

builder.Build().Run();

// Trigger watch restart.

// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}

static string FindObservabilityPath()
{
    foreach (var root in GetObservabilitySearchRoots())
    {
        var sourcePath = Path.Combine(root, "src", "eShop.AppHost", "observability");
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        var localPath = Path.Combine(root, "observability");
        if (Directory.Exists(localPath))
        {
            return localPath;
        }
    }

    throw new DirectoryNotFoundException("Could not find the AppHost observability assets.");
}

static IEnumerable<string> GetObservabilitySearchRoots()
{
    yield return Directory.GetCurrentDirectory();

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        yield return directory.FullName;
        directory = directory.Parent;
    }
}
