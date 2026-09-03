using System.Threading.RateLimiting;
using ApiGateway.Configuration;
using ApiGateway.GraphQL;
using ApiGateway.Health;
using ApiGateway.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Metrics;
using DotNetMonoRepoTemplate.Observability;
using Serilog;
using Yarp.ReverseProxy.Configuration;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var gatewayOptions = ApiGatewayOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(gatewayOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("api-gateway").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateTelemetry("api-gateway");
builder.Services.AddDotNetMonoRepoTemplateMetrics("gateway_");

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<HealthService>();
builder.Services.AddHttpClient<UserProxyClient>(client => client.BaseAddress = new Uri(gatewayOptions.CustomerApiUrl));

var proxyRoutes = new[]
{
    new RouteConfig
    {
        RouteId = "customer-api",
        ClusterId = "customer-api",
        Match = new RouteMatch { Path = "/api/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "admin-api",
        ClusterId = "admin-api",
        Match = new RouteMatch { Path = "/admin/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "scheduler-api",
        ClusterId = "scheduler-api",
        Match = new RouteMatch { Path = "/scheduler/{**catch-all}" },
    },
};

var proxyClusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "customer-api",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destination1"] = new DestinationConfig { Address = gatewayOptions.CustomerApiUrl },
        },
    },
    new ClusterConfig
    {
        ClusterId = "admin-api",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destination1"] = new DestinationConfig { Address = gatewayOptions.AdminApiUrl },
        },
    },
    new ClusterConfig
    {
        ClusterId = "scheduler-api",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destination1"] = new DestinationConfig { Address = gatewayOptions.SchedulerApiUrl },
        },
    },
};

builder.Services.AddReverseProxy().LoadFromMemory(proxyRoutes, proxyClusters);

if (gatewayOptions.GraphQlEnabled)
{
    builder.Services
        .AddGraphQLServer()
        .AddQueryType<Query>()
        .AddMutationType<Mutation>()
        .AllowIntrospection(gatewayOptions.GraphQlIntrospection);
    builder.Services.AddScoped<UserProxyClient>();
}

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(gatewayOptions.CorsOrigin)
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
                PermitLimit = gatewayOptions.RateLimitMax,
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "node-mono-repo-template-chat Gateway",
    Version = "0.1.0",
}));

builder.Services.AddHealthChecks()
    .AddExternalServiceHealthCheck(
        "customer-api",
        async cancellationToken =>
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync($"{gatewayOptions.CustomerApiUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        })
    .AddExternalServiceHealthCheck(
        "admin-api",
        async cancellationToken =>
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync($"{gatewayOptions.AdminApiUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        });

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseRouting();
app.UseDotNetMonoRepoTemplateMetrics();
app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "docs");
}

app.MapDotNetMonoRepoTemplateMetrics();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapServiceHealthEndpoints();

if (gatewayOptions.GraphQlEnabled)
{
    app.MapGraphQL(gatewayOptions.GraphQlPath);
}

app.MapReverseProxy();

app.MapFallback(() => Results.Json(new { error = "Not found" }, statusCode: StatusCodes.Status404NotFound));

var logger = new Logger("ApiGateway");
logger.Info("API Gateway started", new Dictionary<string, object?> { ["port"] = gatewayOptions.Port });

app.Run();
