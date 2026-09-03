using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using DotNetMonoRepoTemplate.Cache;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Metrics;
using DotNetMonoRepoTemplate.Observability;
using ScheduleApi.Auth;
using ScheduleApi.Configuration;
using ScheduleApi.Endpoints;
using ScheduleApi.Jobs;
using ScheduleApi.Middleware;
using ScheduleApi.Services;
using Serilog;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var scheduleApiOptions = ScheduleApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(scheduleApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{scheduleApiOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("schedule-api").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateDatabase(builder.Configuration["DATABASE_URL"] ?? string.Empty);
builder.Services.AddDotNetMonoRepoTemplateCache(
    scheduleApiOptions.RedisUrl,
    !string.Equals(builder.Configuration["REDIS_TLS_REJECT_UNAUTHORIZED"], "false", StringComparison.OrdinalIgnoreCase));
builder.Services.AddDotNetMonoRepoTemplateTelemetry("schedule-api");
builder.Services.AddDotNetMonoRepoTemplateMetrics("schedule_");

builder.Services.AddSingleton(new EmailOptions
{
    MailtrapApiKey = builder.Configuration["MAILTRAP_API_KEY"],
    FromEmail = builder.Configuration["MAILTRAP_FROM"],
    FromName = builder.Configuration["MAILTRAP_FROM_NAME"],
    TestInboxId = builder.Configuration["MAILTRAP_TEST_INBOX_ID"],
});
builder.Services.AddHttpClient<IEmailService, EmailService>();

builder.Services.AddHttpClient(nameof(WebhookProcessorJob));
builder.Services.AddSingleton<WebhookProcessorJob>();
builder.Services.AddHostedService<CronSchedulerHostedService>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(scheduleApiOptions.CorsOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 200 }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Chat API",
        Description = "API documentation for the scheduled endpoints",
        Version = "1.0.0",
    });
    options.AddSecurityDefinition("BearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
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
app.MapFallback(() => Results.Json(new { isSuccessful = false, message = "Not found" }, statusCode: StatusCodes.Status404NotFound));

var logger = new Logger("ScheduleAPI");
logger.Info("Schedule API started", new Dictionary<string, object?> { ["port"] = scheduleApiOptions.Port });

app.Run();
