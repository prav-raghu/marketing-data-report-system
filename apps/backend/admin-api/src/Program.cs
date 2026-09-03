using System.Threading.RateLimiting;
using AdminApi.Auth;
using AdminApi.Configuration;
using AdminApi.Dtos;
using AdminApi.Endpoints;
using AdminApi.Middleware;
using AdminApi.Services;
using AdminApi.Validators;
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
using Serilog;

using var sentry = SentryBootstrapper.Init();

var builder = WebApplication.CreateBuilder(args);

var adminApiOptions = AdminApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(adminApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{adminApiOptions.Port}");

Log.Logger = SerilogBootstrapper.CreateBaseConfiguration("admin-api").CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDotNetMonoRepoTemplateDatabase(builder.Configuration["DATABASE_URL"] ?? string.Empty);
builder.Services.AddDotNetMonoRepoTemplateCache(
    adminApiOptions.RedisUrl,
    !string.Equals(builder.Configuration["REDIS_TLS_REJECT_UNAUTHORIZED"], "false", StringComparison.OrdinalIgnoreCase));
builder.Services.AddDotNetMonoRepoTemplateTelemetry("admin-api");
builder.Services.AddDotNetMonoRepoTemplateMetrics("admin_");

builder.Services.AddSingleton(new EmailOptions
{
    MailtrapApiKey = adminApiOptions.MailtrapApiKey,
    FromEmail = adminApiOptions.MailtrapFrom,
    FromName = adminApiOptions.MailtrapFromName,
    TestInboxId = builder.Configuration["MAILTRAP_TEST_INBOX_ID"],
});
builder.Services.AddHttpClient<IEmailService, EmailService>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BatchOperationService>();
builder.Services.AddScoped<ReportingService>();

builder.Services.AddScoped<IValidator<LoginRequestDto>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<VerifyLoginMfaRequestDto>, VerifyLoginMfaRequestValidator>();
builder.Services.AddScoped<IValidator<ForgotPasswordRequestDto>, ForgotPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ResetPasswordRequestDto>, ResetPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<RefreshTokenRequestDto>, RefreshTokenRequestValidator>();
builder.Services.AddScoped<IValidator<OnboardingRequestDto>, OnboardingRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProfileRequestDto>, UpdateProfileRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequestDto>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<Verify2FARequestDto>, Verify2FARequestValidator>();
builder.Services.AddScoped<IValidator<Disable2FARequestDto>, Disable2FARequestValidator>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(adminApiOptions.CorsOrigin)
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

    options.AddPolicy("adminOperations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 100 }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Admin API", Version = "1.0.0" });
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

app.UseMiddleware<ResponseTimestampMiddleware>();
app.UseMiddleware<AuthGuardMiddleware>();

app.MapDotNetMonoRepoTemplateMetrics();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapBatchEndpoints();
app.MapReportingEndpoints();

app.MapFallback(() => Results.Json(new { isSuccessful = false, message = "Not found" }, statusCode: StatusCodes.Status404NotFound));

var logger = new Logger("AdminAPI");
logger.Info("Admin API started", new Dictionary<string, object?> { ["port"] = adminApiOptions.Port });

app.Run();
