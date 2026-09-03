using System.Threading.RateLimiting;
using CustomerApi.Auth;
using CustomerApi.Configuration;
using CustomerApi.Dtos;
using CustomerApi.Endpoints;
using CustomerApi.Middleware;
using CustomerApi.Services;
using CustomerApi.Validators;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using DotNetMonoRepoTemplate.Cache;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Metrics;
using DotNetMonoRepoTemplate.Observability;
using DotNetMonoRepoTemplate.Types;
using Serilog;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var customerApiOptions = CustomerApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(customerApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{customerApiOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("customer-api").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateDatabase(builder.Configuration["DATABASE_URL"] ?? string.Empty);
builder.Services.AddDotNetMonoRepoTemplateCache(
    customerApiOptions.RedisUrl,
    !string.Equals(builder.Configuration["REDIS_TLS_REJECT_UNAUTHORIZED"], "false", StringComparison.OrdinalIgnoreCase));
builder.Services.AddDotNetMonoRepoTemplateTelemetry("customer-api");
builder.Services.AddDotNetMonoRepoTemplateMetrics("customer_");

builder.Services.AddSingleton(new EmailOptions
{
    MailtrapApiKey = customerApiOptions.MailtrapApiKey,
    FromEmail = customerApiOptions.MailtrapFrom,
    FromName = customerApiOptions.MailtrapFromName,
    TestInboxId = builder.Configuration["MAILTRAP_TEST_INBOX_ID"],
});
builder.Services.AddHttpClient<IEmailService, EmailService>();
builder.Services.AddHttpClient(nameof(WebhookDeliveryService));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WebhookSubscriptionService>();
builder.Services.AddScoped<WebhookDeliveryService>();

builder.Services.AddScoped<IValidator<RegisterRequestDto>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequestDto>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<RefreshTokenRequestDto>, RefreshTokenRequestValidator>();
builder.Services.AddScoped<IValidator<CreateWebhookSubscriptionDto>, CreateWebhookSubscriptionValidator>();
builder.Services.AddScoped<IValidator<UpdateWebhookSubscriptionDto>, UpdateWebhookSubscriptionValidator>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(customerApiOptions.CorsOrigin)
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

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 10 }));

    options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 5 }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Chat API", Version = "1.0.0" });
    options.AddSecurityDefinition("BearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
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

app.UseMiddleware<AuthGuardMiddleware>();

app.MapDotNetMonoRepoTemplateMetrics();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapWebhookEndpoints();
app.MapExportEndpoints();

app.MapFallback(() => Results.Json(new { isSuccessful = false, message = "Not found" }, statusCode: StatusCodes.Status404NotFound));

var logger = new Logger("CustomerAPI");
logger.Info("Customer API started", new Dictionary<string, object?> { ["port"] = customerApiOptions.Port });

app.Run();
