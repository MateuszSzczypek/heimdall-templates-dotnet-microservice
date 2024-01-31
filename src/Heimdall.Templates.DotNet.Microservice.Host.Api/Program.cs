// Create application builder
var builder = WebApplication.CreateBuilder(args);

// Fetch OTLP endpoint from configuration.
var otlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];

// Fetch Microsoft Identity options from configuration.
var microsoftIdentityOptions = builder.Configuration.GetSection("AzureAd").Get<MicrosoftIdentityOptions>() ?? new MicrosoftIdentityOptions();

// Add required services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphBeta"))
    .AddInMemoryTokenCaches();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen();

// Add OpenTelemetry dependencies
builder.Services.AddOpenTelemetry()
    // Configure OpenTelemetry Traces
    .WithTracing(builder =>
    {
        builder.AddSource(Service.Name)
            .ConfigureResource(resource =>
                resource.AddService(
                    Service.Name,
                    serviceVersion: Service.Version))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .ConfigureTraceExporter(otlpEndpoint, microsoftIdentityOptions);
    })
    // Configure OpenTelemetry Metrics
    .WithMetrics(builder =>
    {
        builder.AddMeter(Metrics.RequestMeter.Name)
            .AddMeter(Metrics.EventMeter.Name)
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .ConfigureMeterExporter(otlpEndpoint, microsoftIdentityOptions);
    });

// Configure OpenTelemetry Logs
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;

    var resourceBuilder = ResourceBuilder
        .CreateDefault()
        .AddService(Service.Name);

    logging.SetResourceBuilder(resourceBuilder).ConfigureLoggerExporter(otlpEndpoint, microsoftIdentityOptions);
});

// Build application
var app = builder.Build();

// Add swagger if development mode.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

// Add readiness health check to check EF Core DbContext is connected.
app.MapHealthChecks("/healthz/readiness", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),   
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// Add liveness health check to enable monitoring of application lifecycle.
app.MapHealthChecks("/healthz/liveness", new HealthCheckOptions
{
    Predicate = _ => false
});

// Map controllers routes.
app.MapControllers();

// Log the process id.
app.Logger.LogStarting(Environment.ProcessId);

// Start the application.
app.Run();