using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using DotNetMonoRepoTemplate.Cache;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Ingestion;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Ingestion.Lake;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Metrics;
using DotNetMonoRepoTemplate.Observability;
using IngestionApi.Auth;
using IngestionApi.Configuration;
using IngestionApi.Connectors;
using IngestionApi.Connectors.Meta;
using IngestionApi.Connectors.TikTok;
using IngestionApi.Dtos;
using IngestionApi.Endpoints;
using IngestionApi.Middleware;
using IngestionApi.RateLimiting;
using IngestionApi.Services;
using IngestionApi.Validators;
using Serilog;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var ingestionApiOptions = IngestionApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(ingestionApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{ingestionApiOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("ingestion-api").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateDatabase(builder.Configuration["DATABASE_URL"] ?? string.Empty);
builder.Services.AddDotNetMonoRepoTemplateCache(
    ingestionApiOptions.RedisUrl,
    !string.Equals(builder.Configuration["REDIS_TLS_REJECT_UNAUTHORIZED"], "false", StringComparison.OrdinalIgnoreCase));
builder.Services.AddDotNetMonoRepoTemplateTelemetry("ingestion-api");
builder.Services.AddDotNetMonoRepoTemplateMetrics("ingestion_");

builder.Services.AddDotNetMonoRepoTemplateIngestion(new RawZoneOptions
{
    ConnectionString = ingestionApiOptions.RawZoneConnectionString,
    ContainerName = ingestionApiOptions.RawZoneContainer,
});

builder.Services.AddSingleton(new VendorRateLimiterOptions
{
    PermitsPerWindow = ingestionApiOptions.VendorRateLimitPerMinute,
});
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
builder.Services.AddScoped<IConnectorSecretResolver, ConfigurationSecretResolver>();

builder.Services.AddSingleton(new TikTokOptions
{
    BaseAddress = new Uri(ingestionApiOptions.TikTokBaseUrl),
});
builder.Services.AddHttpClient<ISourceConnector, TikTokAdsConnector>(TikTokApiContract.SourceKey, client =>
{
    client.Timeout = TimeSpan.FromSeconds(ingestionApiOptions.ConnectorTimeoutSeconds);
});

builder.Services.AddSingleton(new MetaOptions
{
    BaseAddress = new Uri(ingestionApiOptions.MetaBaseUrl),
    ApiVersion = ingestionApiOptions.MetaApiVersion,
});
builder.Services.AddHttpClient<ISourceConnector, MetaAdsConnector>(MetaApiContract.SourceKey, client =>
{
    client.Timeout = TimeSpan.FromSeconds(ingestionApiOptions.ConnectorTimeoutSeconds);
});

builder.Services.AddScoped<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddScoped<IIngestionRunService, IngestionRunService>();
builder.Services.AddScoped<IAccountTierService, AccountTierService>();

builder.Services.AddScoped<IValidator<StartRunRequestDto>, StartRunRequestValidator>();
builder.Services.AddScoped<IValidator<CompleteRunRequestDto>, CompleteRunRequestValidator>();
builder.Services.AddScoped<IValidator<FailRunRequestDto>, FailRunRequestValidator>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(ingestionApiOptions.CorsOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = ingestionApiOptions.RateLimitMax,
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ingestion API",
        Description = "Control plane for marketing data ingestion runs and connector configuration",
        Version = "1.0.0",
    });
});

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseRouting();
app.UseDotNetMonoRepoTemplateMetrics();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ApiVersionMiddleware>();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "docs");
}

app.UseMiddleware<ResponseTimestampMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapDotNetMonoRepoTemplateMetrics();
app.MapHealthEndpoints();
app.MapRunEndpoints();
app.MapConnectorEndpoints();
app.MapFallback(() => Results.Json(new { isSuccessful = false, message = "Not found" }, statusCode: StatusCodes.Status404NotFound));

var logger = new Logger("IngestionAPI");
logger.Info("Ingestion API started", new Dictionary<string, object?>
{
    ["port"] = ingestionApiOptions.Port,
    ["reportingTimezone"] = ingestionApiOptions.ReportingTimezone,
    ["rawZoneContainer"] = ingestionApiOptions.RawZoneContainer,
});

app.Run();
