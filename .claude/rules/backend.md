---
paths:
  - "apps/backend/**/*.cs"
---

# Backend Service Rules

You are working on an ASP.NET Core (.NET 10) Minimal API service. These rules apply to all files under `apps/backend/`.

## Non-negotiable

- ASP.NET Core Minimal APIs only — no MVC controllers, no NestJS-style patterns
- FluentValidation for ALL backend validation — never Data Annotations, never Zod on the backend
- Nullable reference types are on and warnings are errors — no `dynamic`, no unjustified `object`, no `#pragma warning disable`, no unjustified `!` null-forgiving operator
- No comments in code
- No hardcoded secrets
- DTOs are `sealed record` types, never mutable classes
- `sealed class` services with explicit access modifiers, constructor-injected dependencies (no service locator, no static mutable state except where the Node original had a deliberate process-global — e.g. `JobHandlerRegistry` in `DotNetMonoRepoTemplate.Queue` — and that's documented as a known simplification, not a pattern to repeat casually)
- No N+1 queries — see `csharp-standards.md` for the full pattern (eager-load with `.Include()`/project up front; never re-query for an entity already loaded and tracked in the same `DbContext`)

## Middleware pipeline order (must not change)

`UseExceptionHandler` → `UseCors` → `SecurityHeadersMiddleware` → `UseRateLimiter` → `UseRouting` → `DotNetMonoRepoTemplate.Metrics`' `UseDotNetMonoRepoTemplateMetrics()` → `RequestLoggingMiddleware` → `ApiVersionMiddleware` → `UseSwagger`/`UseSwaggerUI` (non-production only) → `ResponseTimestampMiddleware` (services that stamp `responseDateTime`, e.g. `admin-api`/`schedule-api`) → `AuthGuardMiddleware` → `app.MapDotNetMonoRepoTemplateMetrics()` → endpoint mapping (`app.Map*Endpoints()`)

Never reorder — `AuthGuardMiddleware` must register after `UseRouting()` so `AllowAnonymous()` endpoint metadata is resolvable when the middleware reads `context.GetEndpoint()`. `RequestLoggingMiddleware` must run before the exception handler has a chance to short-circuit response buffering. `UseDotNetMonoRepoTemplateMetrics()` (prometheus-net's `UseHttpMetrics()`) must run after `UseRouting()` so it can read the matched endpoint's route template for the `http_request_duration_seconds` label, and before `RequestLoggingMiddleware` so a request's timing is captured regardless of what downstream middleware does with it. Endpoints register last via `app.Map<Domain>Endpoints()` extension methods, e.g. `app.MapAuthEndpoints(); app.MapUserEndpoints();` — `app.MapDotNetMonoRepoTemplateMetrics()` (the `/metrics` scrape endpoint) goes immediately before them, matching `api-gateway`'s existing placement.

All four services call `AddDotNetMonoRepoTemplateMetrics("<prefix>_")` in `Program.cs` (right after `AddDotNetMonoRepoTemplateTelemetry(...)`) with a per-service Prometheus metric-name prefix — `gateway_` (api-gateway), `admin_` (admin-api), `customer_` (customer-api), `schedule_` (schedule-api) — so metrics from different services never collide on the same dashboard. This is DI registration + the `/metrics` endpoint only; nothing currently injects `DatabaseMetrics`/`CacheMetrics` into a service to record custom operation-level metrics — that's available infrastructure, not yet exercised.

## Directory structure (immutable)

```
apps/backend/[service-name]/
├── src/
│   ├── Program.cs
│   ├── <Service>.csproj
│   ├── appsettings.json
│   ├── Configuration/
│   │   ├── <Service>Options.cs           # required env-derived properties, no process.env reads elsewhere
│   │   ├── <Service>OptionsValidator.cs  # FluentValidation AbstractValidator<TOptions>
│   │   └── <Service>OptionsFactory.cs    # reads IConfiguration, validates, throws on failure
│   ├── Auth/
│   │   ├── AuthGuardMiddleware.cs
│   │   ├── CurrentUser.cs
│   │   └── RequirePermissionsAttribute.cs
│   ├── Dtos/
│   ├── Validators/
│   │   ├── <Domain>Validators.cs
│   │   └── ValidationResultExtensions.cs # ToBadRequest() — the 400 envelope
│   ├── Services/
│   ├── Middleware/
│   │   ├── SecurityHeadersMiddleware.cs
│   │   ├── RequestLoggingMiddleware.cs
│   │   ├── SensitiveDataMasker.cs
│   │   ├── ApiVersionMiddleware.cs
│   │   ├── AppExceptionHandler.cs        # IExceptionHandler
│   │   └── ResponseTimestampMiddleware.cs # where the Node original had time-stamp-response.plugin.ts
│   └── Endpoints/
│       └── <Domain>Endpoints.cs          # static class, MapGroup + one Map*Endpoints extension method
├── tests/
│   ├── Services/
│   └── <Service>.Tests.csproj
├── Dockerfile
├── .env.example
└── README.md
```

No `Controllers/`, no `Routes/`, no `Schemas/`, no `Guards/` (singular `jwt.guard.ts`-style) — those were Fastify-era folders. Auth is one middleware (`AuthGuardMiddleware`) plus one attribute (`RequirePermissionsAttribute`) reading endpoint metadata, not a per-route guard function.

## Options pattern (environment config)

Every service validates its environment on startup via `<Service>OptionsFactory.Load(builder.Configuration)` in `Program.cs`, called before `WebApplication.CreateBuilder(args).Build()`. Never read `builder.Configuration[...]` or `Environment.GetEnvironmentVariable(...)` directly in a service — inject the resolved `<Service>Options` (registered as a singleton) instead. See `env-config.instructions.md` for the full pattern this replaces (Node's `EnvConfig`).

## Dates — ISO 8601 on the wire, always

Request/response `DateTime` fields serialize as ISO 8601 by default via `System.Text.Json` — never format dates as `dd/MM/yyyy` on the backend, that's a UI-only display concern. See `date-handling.instructions.md` for the full DB → service → UI chain.

## Service registration

All services are registered in `Program.cs` via `builder.Services.AddScoped<TService>()` (or `AddSingleton`/`AddHttpClient<TInterface, TImpl>` where the Node original used a singleton, e.g. `TokenService`, `EmailService`). Endpoints receive them via Minimal API parameter injection — no manual constructor wiring, no service-locator pattern, no `fastify.services` container equivalent.

## Response envelope (always)

```csharp
public sealed record ResponseDto
{
    public required bool IsSuccessful { get; init; }
    public string? Message { get; init; }
    public DateTime? DateTimeStamp { get; init; }
}
```

Concrete DTOs derive from `DotNetMonoRepoTemplate.Types.ResponseDto` and add a typed `Data` property where needed — see `AuthDtos.cs`/`UserDtos.cs` in any ported service for the pattern. `System.Text.Json`'s default camelCase naming policy (ASP.NET Core's default for Minimal APIs) keeps the wire shape identical to the Node era — do not add explicit `[JsonPropertyName]` attributes to match casing, they're unnecessary.

## Validation error format (400 responses)

FluentValidation failures return a structured 400 with field-level errors via `ValidationResultExtensions.ToBadRequest()`:

```json
{
  "isSuccessful": false,
  "message": "Validation failed",
  "errors": [
    { "field": "email", "message": "Must be a valid email address" },
    { "field": "name", "message": "Name is required" }
  ]
}
```

Every endpoint that accepts a body takes an `IValidator<TDto>` as a Minimal API parameter, calls `await validator.ValidateAsync(body)`, and returns `validation.ToBadRequest()` when `!validation.IsValid` — see any `Endpoints/*.cs` file for the pattern. Validators are registered per-DTO in `Program.cs`: `builder.Services.AddScoped<IValidator<LoginRequestDto>, LoginRequestValidator>();`.

## Error status contract

| Scenario | Status |
|---|---|
| Successful create | 201 |
| Successful read/update/delete | 200 |
| FluentValidation failure | 400 |
| Business rule violation | 400 |
| Auth missing | 401 |
| Insufficient permissions | 403 |
| Not found | 404 |
| Unique constraint / duplicate | 409 |
| Partial batch failure | 207 |
| Unexpected error (caught by `AppExceptionHandler`) | 500 |

## FluentValidation rule requirements

- Mirror every constraint the Prisma/AJV schema used to enforce — `NotEmpty()`, `MaximumLength(N)` matching the EF Core entity's `[MaxLength]`/column type, `EmailAddress()`, and any `.Matches()` regex (e.g. the SA phone format `^(\+27|0)[6-8][0-9]{8}$`)
- No open-ended `RuleFor(x => x.SomeObject)` without at least one concrete rule — an empty validator is worse than none, it looks like coverage that isn't there
- See `validation-chain.instructions.md` for the full EF Core → FluentValidation → Zod (frontend) mapping table

## Health and readiness

Every service exposes, both `AllowAnonymous()` and excluded from request/response logging:

```csharp
app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "pong" })).AllowAnonymous();
app.MapGet("/api/v1/ready", async (AppDbContext db, IConnectionMultiplexer redis) =>
{
    await db.Database.ExecuteSqlRawAsync("SELECT 1");
    if (redis.IsConnected) { await redis.GetDatabase().PingAsync(); }
    return Results.Ok(new { status = "ready", db = "ok", redis = "ok" });
}).AllowAnonymous();
```

## Graceful shutdown

Nothing to write by hand — `WebApplication.Run()` already hooks `SIGTERM`/`SIGINT` via `IHostApplicationLifetime` and drains in-flight requests before exiting. Only add an `IHostedService`/lifetime hook if a service owns a resource ASP.NET Core doesn't already manage (a background job loop, a long-lived external connection outside DI's disposal chain).

## Rate limiting tiers

Registered once in `Program.cs` via `builder.Services.AddRateLimiter(...)`, applied per-endpoint with `.RequireRateLimiting("<policy>")` (the global limiter applies to everything else automatically):

| Tier | Limit | Applies to |
|---|---|---|
| `GlobalLimiter` (unnamed, applies by default) | 200 req/min per IP | Every endpoint not opted into a named policy |
| `auth` | 10 req/min per IP | Login, refresh, forgot/reset-password |
| `sensitive` | 5 req/min per IP | 2FA setup/verify/disable, password change, availability checks |
| `adminOperations` | 100 req/min per IP | Admin-tier read endpoints under load (e.g. user-details lookups) |

Not every service needs all four — `schedule-api` and `api-gateway` only need the global limiter; `customer-api` and `admin-api` use the full set. Don't invent a new named tier without checking whether an existing one already fits.

## Before marking complete

Run `dotnet build apps/backend/<service-name>/src/<Service>.csproj` (or the full `DotNetMonoRepoTemplate.sln`) — zero errors, zero warnings required, since nullable warnings are build errors.
