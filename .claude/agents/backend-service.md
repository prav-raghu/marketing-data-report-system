---
name: backend-service
description: Use when working on any ASP.NET Core backend service including api-gateway, customer-api, admin-api, or schedule-api. Covers endpoints, services, DTOs, validators, middleware, authentication, rate limiting, error handling, and general backend business logic. Also activates for refactoring existing services or debugging backend issues. For generating full CRUD endpoints from an EF Core entity, use api-builder instead.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

## Service registry

| Service | Port | Responsibility |
|---|---|---|
| `api-gateway` | 4000 | Public entry point, YARP reverse-proxy routing to the other three services, optional HotChocolate GraphQL layer |
| `customer-api` | 4002 | Customer-facing business logic |
| `admin-api` | 4001 | Administrative operations — JWT + refresh + MFA + RBAC, batch operations, reporting |
| `schedule-api` | 4003 | Scheduling, webhook delivery cron, background jobs |

All four are ASP.NET Core (.NET 10) Minimal API projects — no MVC controllers anywhere in this codebase.

## See also (deep-dive reference docs, read by cross-reference)

- `.claude/instructions/api-versioning.instructions.md` — the `/api/v1/` (and `/api/v2/`) versioning strategy, implemented via `ApiVersionMiddleware`
- `.claude/instructions/graphql.instruction.md` — `api-gateway`'s optional HotChocolate GraphQL layer over the REST microservices (`GraphQL/`), conditionally registered when `GRAPHQL_ENABLED`
- `.claude/instructions/batch-reporting.instruction.md` — `admin-api`'s batch operations and reporting endpoints (`BatchEndpoints.cs`, `ReportingEndpoints.cs`, `BatchOperationService`, `ReportingService`)
- `.claude/instructions/export.instruction.md` — CSV/Excel export via `DotNetMonoRepoTemplate.Export`, used from `admin-api`'s reporting endpoints and `customer-api`'s user export

## Directory structure

Every backend service follows this exact structure:

```
apps/backend/[service-name]/
├── src/
│   ├── Program.cs                  # composition root — DI registration, middleware pipeline, endpoint mapping
│   ├── <Service>.csproj
│   ├── appsettings.json
│   ├── Configuration/
│   │   ├── <Service>Options.cs
│   │   ├── <Service>OptionsValidator.cs
│   │   └── <Service>OptionsFactory.cs
│   ├── Auth/
│   │   ├── AuthGuardMiddleware.cs
│   │   ├── CurrentUser.cs
│   │   └── RequirePermissionsAttribute.cs
│   ├── Dtos/
│   ├── Validators/
│   ├── Services/
│   ├── Middleware/
│   └── Endpoints/
├── tests/
│   └── Services/
├── Dockerfile
├── .env.example
└── README.md
```

Do not alter this structure or naming conventions unless explicitly instructed — see `rules/backend.md` for the full rationale per folder.

## Architecture rules

No custom DI container — ASP.NET Core's built-in `IServiceCollection`/`IServiceProvider` handles everything, registered in `Program.cs` (`builder.Services.AddScoped<TService>()`, `AddSingleton<TOptions>()`, `AddHttpClient<IEmailService, EmailService>()`). Minimal API endpoint delegates receive their dependencies as method parameters — the framework resolves them per-request, no manual constructor wiring anywhere outside `Program.cs`. `AppDbContext` (EF Core), `RedisCacheService`/`IConnectionMultiplexer` (cache), and JWT auth all register the same way — DI-container singletons or scoped services, never a hand-rolled static singleton. DTOs are `sealed record` types, never mutable classes, never `object`/`dynamic`.

## Sentry / observability startup checklist

`DotNetMonoRepoTemplate.Observability`'s `SentryBootstrapper.Init()` is the first statement in `Program.cs`, wrapped in a `using` so it flushes on shutdown:

```csharp
using var sentry = SentryBootstrapper.Init();
var builder = WebApplication.CreateBuilder(args);
```

It's a no-op when `SENTRY_DSN` is unset, so local dev is unaffected. Exceptions reach Sentry through `AppExceptionHandler` (registered via `builder.Services.AddExceptionHandler<AppExceptionHandler>()` and `app.UseExceptionHandler()`), not per-endpoint `catch` blocks — that handler calls `SentryCapture.CaptureException(exception)` once for every unhandled exception. ASP.NET Core keeps one exception handler per app — do not register a second `IExceptionHandler` or a competing `app.Use(...)` try/catch wrapper.

## Endpoint pattern (Minimal API — no controllers)

```csharp
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products");

        group.MapPost("/", async (
            CreateProductDto body,
            IValidator<CreateProductDto> validator,
            ProductService productService,
            HttpContext context) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var currentUser = context.GetCurrentUser();
            var result = await productService.CreateAsync(body, currentUser?.Id ?? "SYSTEM");
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status201Created : StatusCodes.Status400BadRequest);
        });
    }
}
```

No try/catch per endpoint — `AppExceptionHandler` is the single unhandled-exception boundary. A service method that expects a possible business-rule failure returns a `ResponseDto`-derived record with `IsSuccessful = false`, it doesn't throw for control flow.

## Service pattern

```csharp
public sealed class UserService
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;

    public UserService(AppDbContext db, RedisCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetUserAsync(userId);
        if (cached is not null)
        {
            return cached;
        }
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null)
        {
            await _cache.SetUserAsync(user);
        }
        return user;
    }
}
```

`sealed class`, constructor-injected dependencies, `async`/`await` throughout — never `.Result`/`.Wait()` on a `Task`, that deadlocks under ASP.NET Core's synchronization context in ways that only show up under load.

## FluentValidation — never Data Annotations, never Zod on the backend

```csharp
public sealed class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}
```

Register per-DTO in `Program.cs`: `builder.Services.AddScoped<IValidator<CreateProductDto>, CreateProductValidator>();`. DTOs themselves carry no validation attributes — they're plain `sealed record`s; all validation logic lives in the validator class.

## Security rules

JWT authentication via `AuthGuardMiddleware` (bearer token → `TokenService.VerifyAccessToken` → scope check → blacklist check → user lookup → optional `RequirePermissionsAttribute` check). Rate limiting via `Microsoft.AspNetCore.RateLimiting`'s `PartitionedRateLimiter`, configured per-service in `Program.cs`, applied per-endpoint with `.RequireRateLimiting("<policy>")`. All inputs validated via FluentValidation. CORS configured for frontend origins only (`<Service>Options.CorsOrigin`). `SecurityHeadersMiddleware` sets the security headers Helmet used to (X-Content-Type-Options, X-Frame-Options, HSTS, etc.). Never allow auth to be bypassed via query params, headers, or terminal flags. All secrets read through `<Service>Options`, never `Environment.GetEnvironmentVariable` scattered through business logic.

## Enterprise scale (1M+ concurrent users)

Stateless services — no in-memory sessions or local state (ASP.NET Core services are typically `Scoped`/`Singleton` DI registrations, not per-request mutable statics). Horizontally scalable — every service works with N replicas behind a load balancer. Cache-first reads — cache-aside on read-heavy methods via `RedisCacheService`. Queue-backed writes — heavy I/O dispatched via `DotNetMonoRepoTemplate.Queue`'s `JobDispatcher`, never in request handlers (see `common-packages.md` for the honest caveat: this is a Hangfire-backed scaffold confirmed to have zero real Node-era callers, ported as a translation layer, not a battle-tested subsystem). Connection pooling is Npgsql's default pooling via `DATABASE_URL`, tunable via connection-string params if a service needs to override defaults.

Required health endpoints on every service (see `rules/backend.md` for the exact signatures):

```csharp
app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "pong" })).AllowAnonymous();
app.MapGet("/api/v1/ready", async (AppDbContext db, IConnectionMultiplexer redis) => { /* ... */ }).AllowAnonymous();
```

Graceful shutdown is handled by the ASP.NET Core host automatically — see `rules/backend.md`'s "Graceful shutdown" section, nothing to hand-write per service.

Rate limiting tiers — see `rules/backend.md` for the authoritative table (global/`auth`/`sensitive`/`adminOperations`).

Cursor pagination for customer-facing lists:

```csharp
public async Task<CursorResponseDto<TItem>> ListCursorAsync(string? cursor, int take = 20, CancellationToken cancellationToken = default)
{
    var query = _db.Entities.Where(e => e.IsActive).OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id);
    if (cursor is not null)
    {
        query = (IOrderedQueryable<Entity>)query.Where(e => string.Compare(e.Id, cursor) < 0);
    }
    var items = await query.Take(take + 1).ToListAsync(cancellationToken);
    var hasMore = items.Count > take;
    var results = hasMore ? items.Take(take).ToList() : items;
    return new CursorResponseDto<TItem> { IsSuccessful = true, Items = Map(results), NextCursor = hasMore ? results[^1].Id : null, HasMore = hasMore };
}
```

POST endpoints creating resources accept `X-Idempotency-Key`. Propagate `X-Correlation-Id` through downstream calls for distributed tracing — `RequestLoggingMiddleware` already reads and logs it.

## Real-time — not carried over yet

The Node era's optional Socket.IO gateways (`PresenceGateway`, `NotificationGateway`) were never ported — no service in this repo currently needs them, and no real-time transport (SignalR, the ASP.NET Core equivalent) has been evaluated or wired in. If a task genuinely needs real-time push, that's new architectural work requiring the same scrutiny as any first-of-its-kind decision — not a "restore what was there" port, since nothing here was actually restored.

## Testing

xUnit per service under `apps/backend/<service>/tests/` — see `testing.md` (agent) and `rules/testing.md` for the full convention, mock strategy, and coverage expectations. `dotnet test apps/backend/<service>/tests/<Service>.Tests.csproj`.

## Building/running services

```bash
dotnet run --project apps/backend/api-gateway/src/ApiGateway.csproj
dotnet run --project apps/backend/customer-api/src/CustomerApi.csproj
dotnet run --project apps/backend/admin-api/src/AdminApi.csproj
dotnet run --project apps/backend/schedule-api/src/ScheduleApi.csproj
```

Or build the whole solution: `dotnet build DotNetMonoRepoTemplate.sln`.

## Parallel downstream calls

Not permitted due to system constraints — all downstream service calls must be sequential (`await` one at a time, not `Task.WhenAll` fan-out), same constraint as the Node era, now enforced the same way in C# — don't reach for `Task.WhenAll` on cross-service HTTP calls without checking this still holds.
