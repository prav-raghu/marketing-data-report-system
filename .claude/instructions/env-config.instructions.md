---
applyTo: "apps/backend/**/Configuration/**"
description: "Environment variable validation — FluentValidation-validated Options pattern in every backend service"
---

# Environment Variable Configuration

Every backend service that reads environment variables must do so through its `<Service>Options` class, resolved once at startup and validated with FluentValidation. Never read `Environment.GetEnvironmentVariable`/`IConfiguration[...]` directly in service or endpoint code — inject the resolved `<Service>Options` instead (registered as a DI singleton). This replaces the Node era's AJV-validated `EnvConfig` static class — the shared `common/config` package it lived in was never ported (confirmed dead code even before the migration) and does not exist in this codebase.

## Pattern — three files per service, in `Configuration/`

**`<Service>Options.cs`** — the resolved, typed shape:

```csharp
public sealed record CustomerApiOptions
{
    public required string JwtSecret { get; init; }
    public required string JwtRefreshSecret { get; init; }
    public required string RedisUrl { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }
    public int? AccountBanThreshold { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
```

Required env vars are `required` properties (compile-time enforced that the factory sets them); genuinely optional ones are nullable. Computed convenience properties (like `IsProduction`) belong here, not scattered as string comparisons through the codebase.

**`<Service>OptionsValidator.cs`** — a `FluentValidation.AbstractValidator<TOptions>`:

```csharp
public sealed class CustomerApiOptionsValidator : AbstractValidator<CustomerApiOptions>
{
    public CustomerApiOptionsValidator()
    {
        RuleFor(x => x.JwtSecret).NotEmpty().MinimumLength(32);
        RuleFor(x => x.JwtRefreshSecret).NotEmpty().MinimumLength(32);
        RuleFor(x => x.RedisUrl).NotEmpty();
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.NodeEnv).Must(value => value is "development" or "production");
    }
}
```

**`<Service>OptionsFactory.cs`** — reads `IConfiguration`, validates, throws on failure:

```csharp
public static class CustomerApiOptionsFactory
{
    public static CustomerApiOptions Load(IConfiguration configuration)
    {
        var options = new CustomerApiOptions
        {
            JwtSecret = configuration["JWT_SECRET"] ?? string.Empty,
            JwtRefreshSecret = configuration["JWT_REFRESH_SECRET"] ?? string.Empty,
            RedisUrl = configuration["REDIS_URL"] ?? string.Empty,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            AccountBanThreshold = int.TryParse(configuration["ACCOUNT_BAN_THRESHOLD"], out var threshold) ? threshold : null,
        };

        var result = new CustomerApiOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
```

## Wiring into `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);
var customerApiOptions = CustomerApiOptionsFactory.Load(builder.Configuration);
builder.Services.AddSingleton(customerApiOptions);
builder.WebHost.UseUrls($"http://0.0.0.0:{customerApiOptions.Port}");
```

`Load()` is called once, before `builder.Build()` — if required vars are missing or invalid, the process fails fast at startup with a clear error, before accepting any traffic. `IConfiguration` (ASP.NET Core's built-in configuration system) already reads environment variables, `appsettings.json`, and (in development) `.env`-loaded values through the standard provider chain — nothing extra needs registering for that part.

## Usage in a service

```csharp
public sealed class TokenService
{
    private readonly SymmetricSecurityKey _accessKey;

    public TokenService(CustomerApiOptions options, IConnectionMultiplexer redis)
    {
        _accessKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret));
    }
}
```

`CustomerApiOptions` is injected like any other DI-registered singleton — services take it as a constructor parameter, they don't call a static `.Get()`/`.Load()` accessor the way the old `EnvConfig` did.

## Rules

- `<Service>OptionsFactory.Load()` runs once at startup — if required vars are missing or fail validation, the process throws immediately and never starts serving traffic
- Every service's `Configuration/<Service>Options.cs` only declares the vars *that service* actually reads — there's no shared cross-service options base class, since each service's real env-var set differs (compare `AdminApiOptions`'s `TwoFactorEncryptionKey`/`PasswordResetExpirationMinutes` to `ScheduleApiOptions`'s simpler API-key-only shape)
- Never use `configuration["SOME_VAR"] ?? "fallback"` in service or endpoint code — only inside the `<Service>OptionsFactory`
- Every required var must be in both `.env` (real values, gitignored, developer-created) and `.env.example` (placeholders, committed)
- `NODE_ENV` is still read (alongside `DOTNET_ENVIRONMENT`) for app-level environment semantics like `IsProduction` — ASP.NET Core's own `IHostEnvironment.EnvironmentName` (driven by `DOTNET_ENVIRONMENT`) governs framework-level behavior (which config files load, whether developer exception pages show); the two are set together in `.env.example`, not interchangeable
