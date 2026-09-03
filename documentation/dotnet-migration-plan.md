# Node.js/Fastify → .NET Core/C# Migration Plan

## Purpose

This document plans the migration of `apps/backend/*` and `common/*` from
Node.js/TypeScript/Fastify to **.NET 10 (LTS) / C#**, while keeping every
*frontend* app (`admin-web`, `customer-web`, `customer-mobile`, `apps/cms`)
untouched and talking to the new backend over the exact same HTTP/GraphQL
contracts they use today.

The goal is not "rewrite in C#, figure out the rest as we go." The goal is:
**port the ideology** — the same layering, the same non-negotiable rules,
the same request lifecycle, the same RBAC/JWT/audit/webhook/feature-flag
patterns — into idiomatic .NET, so a developer who knows this repo's Node
conventions can read the C# and recognize every decision.

Nothing in this document authorizes running database migrations or git
pushes beyond what's already been asked — those still require explicit
sign-off per `CLAUDE.md`.

---

## 1. Guiding principle: same ideology, new language

Every non-negotiable rule in `CLAUDE.md` has a direct .NET equivalent. None
of these are up for reinterpretation during the port — a rule that says
"never do X" in TypeScript still means "never do X" in C#.

| Node/TS rule | .NET/C# equivalent |
|---|---|
| Fastify v5 vanilla, no NestJS/Express | **ASP.NET Core Minimal APIs**, no MVC-heavy conventions, no third-party web framework |
| AJV only for backend validation, never Zod | **FluentValidation** only — never Data Annotations attributes scattered on DTOs, never ad-hoc manual checks |
| TypeScript strict, no `any`/`as unknown as T`/`@ts-ignore` | **Nullable reference types enabled solution-wide**, `TreatWarningsAsErrors`, no `dynamic`, no `#pragma warning disable`, no `!` null-forgiving operator outside justified EF Core navigation properties |
| DTOs are interfaces only, never classes | DTOs are **C# `record` types** (immutable, value equality) — never mutable classes |
| No comments in code | Same rule, unchanged |
| No hardcoded secrets | Same rule — `IConfiguration`/environment variables only, never `appsettings.json` for secrets |
| Folder structure immutable | New structure below is equally immutable once adopted |
| `common/` for shared packages | Shared **class libraries** under `common/`, one `.csproj` per package, referenced via `ProjectReference` (the .NET analogue of `workspace:*`) |
| All packages scoped `@node-mono-repo-template/[name]` | All class libraries namespaced `DotNetMonoRepoTemplate.[Name]`, NuGet `PackageId` prefixed the same way if ever published |
| Class-based controllers/services, explicit access modifiers | Same — C# endpoint classes and service classes, `public`/`private`/`internal` always explicit, never implicit |
| Match existing style, don't switch idioms mid-file | Same discipline applies once the C# style is set — no mixing minimal-API lambdas with MVC controllers in the same service |
| Service layer needs unit tests in `/tests/services` | **`tests/Services/`** — xUnit, same coverage bar |
| Dockerfiles per app root | Same — `apps/backend/<service>/Dockerfile`, multi-stage .NET builds |
| Root `docker-compose.yaml` is the single deploy stack | Unchanged in shape — image builds change, compose topology doesn't |
| Don't run migrations without being told | Same — `dotnet ef migrations` / `dotnet ef database update` gated identically to `prisma migrate` |
| Don't run git ops without being told | Unchanged |

---

## 2. Technology mapping (what replaces what)

| Concern | Node.js today | .NET replacement | Notes |
|---|---|---|---|
| Web framework | Fastify v5 | ASP.NET Core 8 Minimal APIs | Endpoint groups (`MapGroup`) replace `fastify.register(routes, { prefix })` |
| Language/runtime | TypeScript / Node 22 | C# 14 / .NET 10 (LTS) | .NET 10 (released Nov 2025) is the current LTS as of this plan; .NET 8 is in maintenance and loses support Nov 2026 — too soon for a multi-month migration, see §17 |
| ORM | Prisma | **Entity Framework Core 10** + Npgsql provider | `schema.prisma` → EF Core `DbContext` + entity classes + Fluent API configuration |
| Validation | AJV (JSON Schema) | **FluentValidation** | `AbstractValidator<T>` per DTO, mirrors AJV's per-field rule granularity |
| Auth tokens | `jsonwebtoken` (HS256) | **System.IdentityModel.Tokens.Jwt** (`JwtSecurityTokenHandler`), HS256 preserved | Denylist/minIat/refresh-rotation logic is hand-rolled today in Node — ported as hand-rolled C# service, not swapped for ASP.NET Identity's cookie model |
| Password hashing | `bcrypt` | **BCrypt.Net-Next** | Same algorithm, same cost factor (10) |
| TOTP (2FA) | `otplib` | **Otp.NET** | Same RFC 6238 TOTP, same secret encryption pattern (AES-256-GCM via `System.Security.Cryptography`) |
| Redis client | `ioredis` | **StackExchange.Redis** | `REDIS_URL`-only convention preserved |
| Queue/jobs | BullMQ (Redis-backed) | **Hangfire** (Redis storage) or **MassTransit + Redis/RabbitMQ** | Hangfire recommended — closest conceptual match to BullMQ (dashboard, retries, delayed jobs) without introducing a new broker |
| GraphQL gateway | Apollo Server + `@apollo/gateway` (schema stitching) | **HotChocolate** with Fusion/stitching, or keep api-gateway routing pure REST and drop federation if unused in practice | Decision point — see §7 |
| Logging | Winston/pino via `common/logging` + OpenTelemetry | **Serilog** + `OpenTelemetry.Extensions.Logging` | Structured logging, same log-shape contract (`{ level, message, context, timestamp }`) |
| Metrics | `prom-client`-based `common/metrics` | **prometheus-net** | Same `/metrics` endpoint contract, same collector categories (http, node/process, custom) |
| Tracing/errors | OpenTelemetry SDK + Sentry (`common/observability`) | **OpenTelemetry .NET** + **Sentry SDK for .NET** | Same OTLP exporter target, same Sentry DSN env var |
| Email | Nodemailer/Mailtrap API (`common/email`) | **MailKit** (SMTP) or Mailtrap's REST API via `HttpClient` | Template rendering: Handlebars-style templates → **Scriban** or **RazorLight** |
| SMS | SMSPortal REST client (`common/sms`) | Same REST API via typed `HttpClient` | No SDK change needed — it's just HTTP calls either way |
| Storage | AWS SDK v3 (S3/R2) + Azure Blob SDK (`common/storage`) | **AWSSDK.S3** + **Azure.Storage.Blobs** | 1:1 SDK equivalents exist for both |
| Export | ExcelJS/csv-writer (`common/export`) | **ClosedXML** (Excel) + **CsvHelper** (CSV) | |
| Config/env validation | AJV-schema env validation (`common/config`, `EnvConfig`) | **Options pattern** (`IOptions<T>`) with **FluentValidation** or `IValidateOptions<T>` validating on startup | Fail-fast on boot, same as today's `EnvConfig.get()` |
| API docs | `@fastify/swagger` | **Swashbuckle.AspNetCore** or **NSwag** | OpenAPI 3 output, same versioned-spec convention |
| Testing | Jest | **xUnit** + **FluentAssertions** + **NSubstitute** (mocking) | `tests/services`, `tests/integration` structure preserved |
| Test DB | Real Postgres via Docker in CI | **Testcontainers for .NET** (`Testcontainers.PostgreSql`) | Same "integration tests hit a real Postgres" philosophy |
| Package manager | pnpm workspaces | **.NET solution (`.sln`) + project references**, `Directory.Packages.props` for central version management | Central package management ≈ pnpm's single lockfile discipline |
| Monorepo task runner | Turborepo | **`dotnet build`/`dotnet test` at solution level**, optionally `Directory.Build.props` for shared settings | No strict Turborepo equivalent needed — MSBuild's project graph already gives incremental builds |
| Linting/formatting | ESLint + Prettier | **`dotnet format`** + **.editorconfig** + Roslyn analyzers (`Microsoft.CodeAnalysis.NetAnalyzers`, StyleCop if desired) | Enforced in CI the same way `pnpm lint`/`pnpm format:check` is today |

---

## 3. New folder structure (mirrors the immutable Node layout)

```text
apps/backend/                  (unchanged path — services rewritten in C#)
  admin-api/
    src/
      AdminApi.csproj
      Program.cs                  ← application.ts + main.ts equivalent
      Configuration/
        EnvOptions.cs              ← config/env.config.ts
        RateLimitOptions.cs
      Endpoints/
        AuthEndpoints.cs           ← routes/auth.route.ts + controllers/auth.controller.ts
        UserEndpoints.cs
        BatchEndpoints.cs
        ReportingEndpoints.cs
        HealthEndpoints.cs
      Services/
        AuthService.cs
        UserService.cs
        TokenService.cs
        BatchOperationService.cs
        ReportingService.cs
      Dtos/
        AuthDtos.cs                 ← one file per domain, records only
        UserDtos.cs
        PaginationDtos.cs
      Validators/
        UserValidators.cs           ← FluentValidation, one class per DTO
        AuthValidators.cs
      Middleware/
        ErrorHandlingMiddleware.cs  ← plugins/error-handler.plugin.ts
        RequestLoggingMiddleware.cs
        ApiVersionMiddleware.cs
        TimestampResponseMiddleware.cs
      Auth/
        JwtAuthHandler.cs           ← guards/auth.guard.ts (custom AuthenticationHandler)
      Extensions/
        ServiceCollectionExtensions.cs   ← plugins/services.plugin.ts (DI wiring)
        SwaggerExtensions.cs
    tests/
      Services/
      Integration/
      Factories/
    Dockerfile
    appsettings.json
    appsettings.Development.json
    .env.example
    README.md
common/
  DotNetMonoRepoTemplate.Database/       ← common/database (EF Core DbContext + entities)
  DotNetMonoRepoTemplate.Cache/          ← common/cache (StackExchange.Redis wrapper)
  DotNetMonoRepoTemplate.Config/         ← common/config (Options pattern base)
  DotNetMonoRepoTemplate.Logging/        ← common/logging (Serilog setup)
  DotNetMonoRepoTemplate.Metrics/        ← common/metrics (prometheus-net)
  DotNetMonoRepoTemplate.Observability/  ← common/observability (OTel + Sentry)
  DotNetMonoRepoTemplate.Queue/          ← common/queue (Hangfire wrapper)
  DotNetMonoRepoTemplate.Email/          ← common/email (MailKit)
  DotNetMonoRepoTemplate.Sms/            ← common/sms
  DotNetMonoRepoTemplate.Storage/        ← common/storage
  DotNetMonoRepoTemplate.Export/         ← common/export
  DotNetMonoRepoTemplate.Types/          ← common/types (shared DTOs, RBAC enums/tables, constants)
  DotNetMonoRepoTemplate.Utilities/      ← common/utilities
DotNetMonoRepoTemplate.sln
Directory.Build.props                  ← shared TFM, nullable, analyzers, LangVersion
Directory.Packages.props               ← central NuGet version pins
.editorconfig
```

Each backend service keeps its **own `.csproj`**, exactly as it keeps its
own `package.json` today. Shared code only ever moves into `common/` — a
service project must never `ProjectReference` another service project,
mirroring the existing "no cross-service imports" boundary.

---

## 4. Concrete pattern translation (proof this is the same ideology)

To ground the mapping table, here's `admin-api`'s user-onboarding path
translated end-to-end, matching today's Fastify/Prisma/AJV files
(`controllers/user.controller.ts`, `services/user.service.ts`,
`dtos/user.dto.ts`, `schemas/user.schema.ts`).

**DTO** (`Dtos/UserDtos.cs`) — record, not class, mirrors the "interfaces
only" rule:

```csharp
public sealed record OnboardingRequestDto(
    string Username,
    string Email,
    string Password,
    string RoleId,
    string UserStatusId,
    string? Gender,
    int? Age,
    bool AcceptTermsAndConditions,
    bool AllowEmailCommunications);

public sealed record OnboardingResponseDto(
    bool IsSuccessful,
    string Message,
    object? Data = null);
```

**Validator** (`Validators/UserValidators.cs`) — FluentValidation is the
AJV analogue: field-level rules, no `additionalProperties` needed because
C# record binding is already closed to unknown properties.

```csharp
public sealed class OnboardingRequestValidator : AbstractValidator<OnboardingRequestDto>
{
    public OnboardingRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.UserStatusId).NotEmpty();
    }
}
```

**Service** (`Services/UserService.cs`) — same constructor-injection
pattern as today (no DI-framework magic beyond ASP.NET Core's built-in
container, which plays the exact role `plugins/services.plugin.ts` plays
now):

```csharp
public sealed class UserService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public UserService(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<OnboardingResponseDto> OnboardUserAsync(
        OnboardingRequestDto model, CancellationToken ct)
    {
        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == model.Username || u.Email == model.Email, ct);

        if (existingUser is not null)
        {
            var message = existingUser.Username == model.Username
                ? "Username already exists"
                : "Email already exists";
            return new OnboardingResponseDto(false, message);
        }

        var authHash = Guid.NewGuid().ToString();
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = model.Username,
            Email = model.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 10),
            RoleId = model.RoleId,
            UserStatusId = model.UserStatusId,
            GenderId = model.Gender,
            Age = model.Age,
            AcceptTermsAndConditions = model.AcceptTermsAndConditions,
            AllowEmailCommunications = model.AllowEmailCommunications,
            AuthHash = authHash,
            AuthHashExpiration = DateTime.UtcNow.AddHours(24),
            IpAddress = string.Empty,
            CreatedBy = "SYSTEM",
            ModifiedBy = "SYSTEM",
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _emailService.SendMailAsync(
            user.Email, "Admin Onboarding", "admin-onboarding-notification",
            new { username = user.Username, verificationLink = $"{_adminWebUrl}/verify-email?auth_hash={authHash}" });

        return new OnboardingResponseDto(true, "User onboarded successfully");
    }
}
```

**Endpoint** (`Endpoints/UserEndpoints.cs`) — Minimal API group replaces
`routes/user.route.ts` + `controllers/user.controller.ts`'s try/catch
wrapper. The response-envelope and error-status contract from
`.claude/rules/backend.md` is unchanged:

```csharp
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users");

        group.MapPost("/onboard", async (
            OnboardingRequestDto body,
            IValidator<OnboardingRequestDto> validator,
            UserService userService,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(body, ct);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new
                {
                    isSuccessful = false,
                    message = "Validation failed",
                    errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
                });
            }

            var result = await userService.OnboardUserAsync(body, ct);
            return Results.Json(result, statusCode: result.IsSuccessful ? 200 : 400);
        });
    }
}
```

Same envelope (`{ isSuccessful, data?, message?, errors? }`), same status
contract (400 on validation/business failure, 401/403/404/409/500 exactly
as tabled in `backend.md`), same "service returns a result object, endpoint
just maps it to a status code" split of responsibility. The try/catch that
today lives per-controller-method moves to **one global exception-handling
middleware** (`Middleware/ErrorHandlingMiddleware.cs`), which is actually
closer to the framework-level intent than Fastify's per-method try/catch —
call this out to the team as a deliberate improvement, not a divergence in
philosophy.

---

## 5. Cross-cutting patterns — how each non-trivial subsystem ports

### 5.1 JWT / token lifecycle (`jwt-security` ideology)

`TokenService` in Node hand-rolls access/refresh tokens with a Redis-backed
denylist, per-user `minIat` marker for "log out everywhere," and refresh
rotation. None of that is delegated to a framework abstraction in Node, and
it shouldn't be in .NET either — **do not reach for ASP.NET Core Identity's
cookie/session model**, port `TokenService` as a hand-rolled C# class:

- `JwtSecurityTokenHandler` (`System.IdentityModel.Tokens.Jwt`) for
  sign/verify, HS256, same secret env vars (`JWT_SECRET`,
  `JWT_REFRESH_SECRET`).
- `IConnectionMultiplexer` (StackExchange.Redis) replaces `ioredis` for the
  three key prefixes: `token:blacklist:`, `token:refresh:`, `token:minIat:`
  — same TTLs (1h access, 1d/30d refresh depending on "remember me", 30d
  refresh-blacklist).
- `jti` claim → blacklist check → `minIat` session-invalidation check →
  user lookup, in that exact order, ported into a custom
  `AuthenticationHandler<JwtAuthSchemeOptions>` (the .NET analogue of
  `guards/auth.guard.ts` registered as a Fastify `onRequest` hook).
- MFA two-step flow (`mfaToken` short-lived token issued on
  password-success, real tokens only after `/auth/verify-login-mfa`)
  ports as-is — this is business logic in the service layer, not framework
  behavior, so it moves almost line-for-line.
- Logout-everywhere semantics (`minIat` marker) — unchanged design,
  ported 1:1.

### 5.2 RBAC (`rbac` ideology)

`common/types/src/rbac.ts`'s `RolePermissions` static map and
`roleHasPermission()` helper port directly to a C# static
`Dictionary<RoleName, IReadOnlySet<Permission>>` plus an extension method,
living in `DotNetMonoRepoTemplate.Types`. Permission checks in endpoints
become a small `RequirePermission(Permission.UserWrite)` endpoint filter
(`IEndpointFilter`) — the .NET equivalent of the guard-as-Fastify-hook
pattern, evaluated after the `JwtAuthHandler` populates
`HttpContext.User` claims (role + permissions serialized as claims, same
as they're embedded in the JWT payload today).

### 5.3 Database (`relational-database`/`prisma` ideology → EF Core)

- `schema.prisma` → EF Core entity classes + `AppDbContext` with Fluent API
  configuration (`OnModelCreating`), **not** Data Annotations on entities —
  keeps schema definition centralized the way `schema.prisma` is today.
- Every model keeps the six base metadata fields
  (`Id`, `IsActive`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `ModifiedBy`) —
  same two sanctioned exceptions (append-only audit rows, system-mutated
  rows) documented in `.claude/rules/prisma.md` carry over verbatim to an
  EF Core equivalent rules doc.
- `@map`/`@@map` snake_case column/table mapping → EF Core's
  `UseSnakeCaseNamingConvention()` (via `EFCore.NamingConventions` package)
  or explicit `.ToTable("users")` / `.HasColumnName("created_at")` per
  property — pick one approach repo-wide, don't mix.
- `@@index` → Fluent API `.HasIndex(...)`, including the composite
  cursor-pagination and common-query-pattern indexes already documented in
  `prisma.md`.
- Migrations: `dotnet ef migrations add <Name>` replaces
  `prisma migrate dev` — **the developer still runs this**, same rule as
  today, Claude never runs `dotnet ef database update` unasked.
- `common/database/prisma/seed.ts` → a `DbSeeder` class invoked from a
  dedicated console entry point or a startup-gated `IHostedService` (dev
  only).

### 5.4 Audit log, webhook events, feature flags

All three are DB-backed patterns layered on top of the base service/DTO
pattern in Node (not framework features) — they port with no architectural
change, just C#:

- **Audit log**: the "before/after value diff on state-changing ops"
  pattern becomes an `IAuditLogger` service injected into any service that
  mutates state, called explicitly at the same call sites — not an EF Core
  interceptor doing it invisibly, to keep the same "explicit, not magic"
  philosophy the Node agent enforces.
- **Webhook events**: `webhook_subscription`/`webhook_delivery` Prisma
  models port directly to EF Core entities (already meet the six-field
  rule per `prisma.md`'s documented exception). The delivery worker
  (retry/backoff, HMAC signing via `webhook-signature.utility.ts`) ports to
  a Hangfire recurring/background job; HMAC signing uses
  `System.Security.Cryptography.HMACSHA256`, same signature header
  contract so existing external subscribers don't need to change anything.
- **Feature flags**: DB-backed flag store ports to an EF Core entity +
  `IFeatureFlagService.IsEnabledAsync(flagKey, context)`, same evaluation
  shape, optionally fronted by `Microsoft.FeatureManagement` if the team
  wants the framework-level ergonomics — evaluate this as a "nice to have,"
  not a requirement, since the current Node implementation is already
  bespoke.

### 5.5 GraphQL gateway (`api-gateway`) — RESOLVED

Read the actual code before Phase 3 started, and the "spike" this section
originally called for turned out to be unnecessary — the answer was
already in the source. `api-gateway` does **not** run Apollo Federation
or schema stitching across services despite `@apollo/gateway` and
`@graphql-tools/stitch` sitting in its `package.json`. The real
implementation (`graphql.plugin.ts` → `schema-builder.ts` →
`user.resolvers.ts`) is a single local `makeExecutableSchema` with one
resolver set (`UserResolvers`) that proxies plain HTTP calls to
**`customer-api` only** — `admin-api` and `schedule-api` are never
touched by the GraphQL layer despite the config accepting URLs for all
three. It's also feature-flagged off by default
(`GRAPHQL_ENABLED !== 'true'`).

So: no federation was ever running, which resolves the decision — **port
it as a single-schema GraphQL server with HTTP-proxying resolvers**,
using **HotChocolate** (the .NET GraphQL server), matching the actual
shape of the Node code instead of the aspirational package list. The
core reverse-proxy responsibility (`/api`→`customer-api`, `/admin`→
`admin-api`, `/scheduler`→`schedule-api`, all prefix-preserving) is a
separate, always-on concern from the GraphQL layer and is ported with
**YARP** (Microsoft's own reverse-proxy library) — the direct .NET
analogue of `@fastify/http-proxy`, and arguably a better fit than
Fastify's proxy plugin since YARP is purpose-built for exactly this.

---

## 6. Phased migration plan (strangler-fig, not big-bang)

The `api-gateway` already sits in front of `admin-api`/`customer-api`/
`schedule-api` — that makes it the natural seam for a **strangler-fig**
migration: bring up .NET services one at a time behind the same gateway,
route traffic to them, and only decommission the Node service once the
.NET replacement has run in parallel and passed a verification bar.

| Phase | Scope | Exit criteria |
|---|---|---|
| **0 — Foundation** | Stand up `DotNetMonoRepoTemplate.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, CI job that runs `dotnet build`/`dotnet test`/`dotnet format --verify-no-changes` alongside (not replacing) the existing Node CI. Pick and spike the GraphQL gateway decision (§5.5). | Empty solution builds in CI; team has agreed the C# style guide (this doc + a short `.claude`-equivalent ruleset, see §8) |
| **1 — Common packages** | Port `common/types`, `common/config`, `common/utilities`, `common/database` (schema + `AppDbContext`, **pointed at the same Postgres instance and the same tables** — no new migration, no schema drift, EF Core reads what Prisma wrote), `common/cache`, `common/logging` | Class libraries build, unit-tested, `AppDbContext` round-trips reads/writes against the live schema without any DDL change |
| **2 — Lowest-risk service first** | Pick the smallest/least-trafficked service — likely `schedule-api` (API-key auth, no user-facing JWT/RBAC/MFA surface) — port fully to .NET, deploy behind the gateway alongside the Node version, cut a small % of traffic (or a specific route) to it | .NET `schedule-api` matches Node `schedule-api` on every existing integration test rerun against it; error rates and latency at parity in production for an agreed soak period |
| **3 — Gateway** | Resolve §5.5, port `api-gateway` itself, since every later service needs a stable place to route into | Gateway routes to a mix of Node and .NET upstream services transparently; frontend apps see no contract change |
| **4 — customer-api** | Port controllers/services/DTOs/validators for the customer-facing domain, including JWT (no MFA on this surface per current code, confirm) and RBAC | Full integration test suite (ported to xUnit + Testcontainers) green; parallel-run soak period at parity |
| **5 — admin-api** | Highest-risk service: full JWT+refresh+MFA+RBAC+2FA+audit-log surface, batch operations, reporting/export. Port last, after JWT/RBAC/audit patterns are already proven in `customer-api` | Same bar as Phase 4, plus a manual security pass on the auth surface specifically (mirrors what `jwt-security` subagent would review in Node) |
| **6 — Cross-cutting hardening** | Metrics, tracing, Sentry, webhook delivery worker, feature flags, export jobs — anything that was "good enough riding along with the services" gets its own verification pass once all three services are on .NET | Dashboards (Grafana/Prometheus, Sentry) show equivalent signal for .NET services as they did for Node |
| **7 — Decommission** | Remove Node service source, Node Dockerfiles, Node entries from `docker-compose.yaml`/Coolify, Node CI jobs, `common/*` TS packages once nothing references them | Node backend fully removed from the deploy stack; `pnpm-workspace.yaml` no longer lists `apps/backend/*` |

Each phase-2-through-5 service migration should itself follow a **shadow →
partial → full cutover** pattern per service, not a flag day: deploy the
.NET service, mirror/replay a slice of real traffic to it in shadow mode
(compare responses, don't serve them), then cut over a small percentage of
live traffic behind the gateway, then ramp to 100%, then decommission the
Node twin. This is a deployment-pipeline detail for `deployment-coolify`/
`infrastructure` to work out per-service, not something to improvise ad
hoc per phase.

### 6.1 Policy change: Node source deleted per-service, not held for Phase 7

**Superseded by explicit instruction, recorded here for anyone reading
this plan cold.** The original framing above — shadow traffic, soak
periods, decommission only as a final Phase 7 — assumed the team wanted
a real parallel-run verification window per service before deleting
anything. The team instead asked for old TypeScript to be removed as
soon as each piece is migrated, not held until a verified cutover. That
changes the risk posture, worth stating plainly rather than leaving the
table above looking followed when it isn't:

- **What actually happened**: as of Phase 4 completing, the Node/
  TypeScript source for `schedule-api`, `api-gateway`, and `customer-api`
  was deleted **immediately** — `package.json`, `tsconfig*.json`,
  `jest.config.ts`, the old `Dockerfile`, `tests/`, and every `.ts` file
  under `src/` — leaving only the .NET implementation in each service
  directory. The five `common/*` TS packages that had zero remaining
  Node consumers after that (`sms`, `storage`, `queue`, `config`,
  `metrics` — confirmed by grepping the whole repo for their import
  specifiers before deleting) were removed the same way. The eight
  `common/*` TS packages `admin-api` still imports (`cache`, `database`,
  `email`, `export`, `logging`, `observability`, `types`, `utilities`)
  are **not** deleted yet — deleting them now would break the
  still-running Node `admin-api`, which is the one Node backend service
  left. They're deleted once Phase 5 removes that last consumer.
- **What this means concretely**: there is no working Node version of
  `schedule-api`, `api-gateway`, or `customer-api` left in this
  repository to fall back to if the .NET port has a bug — only git
  history. That history is a real safety net (`git revert`/`git log` on
  this branch recovers every deleted file exactly), but it's a slower
  recovery path than "the old service is still sitting right there,
  deployed and running." Since **nothing in this migration has been
  compiled yet** (no .NET SDK in this sandbox — see §14), this is a
  meaningfully different risk position than the phase table above
  describes: normally you'd want a green build and a soak period *before*
  removing the fallback, not after deleting it sight-unseen.
- **Why this is still a reasonable call**: it's an explicit, informed
  instruction, not a default this plan chose on its own — and because
  everything lives in git, "delete now" is a much weaker commitment than
  it sounds. If the developer's first local `dotnet build`/`dotnet run`
  turns up a real bug in one of these three services, the fix is either
  to patch it forward in C# or `git checkout` the pre-deletion commit for
  that service's Node source and keep it running a little longer — both
  are cheap. The exit-criteria columns in the phase table above (parity
  soak periods, shadow traffic) describe the *cautious* version of this
  migration; what's actually being executed is the *fast* version, on
  request, with git as the safety net instead of a parallel-run window.
- **Sequencing implication**: Phase 7 in the table above is no longer a
  separate future phase — it's happening continuously, one service at a
  time, immediately after that service's C# port is written. The phase
  table is kept as-is (rather than rewritten) because it's still useful
  as a description of what a more cautious version of this migration
  would look like, and because the *order* of phases (foundation →
  common → schedule-api → gateway → customer-api → admin-api → hardening)
  is unchanged — only the "wait for verification before deleting" part
  was dropped.
- **Cleanup done alongside the deletions**: root `package.json`'s
  `dev:gateway`/`dev:customer-api`/`dev:schedule-api`/`debug:api-gateway`/
  `debug:customer-api`/`debug:schedule-api` scripts removed (nothing left
  for them to run). `pnpm-workspace.yaml` needed no change — it's
  glob-based and simply stops matching directories once their
  `package.json` is gone. `turbo.json` had no explicit per-service
  entries to update. `.github/workflows/continuous-integration.yml`'s
  only references to these three services were already commented out, so
  nothing there could break. `docker-compose.yaml` still references
  `ghcr.io/node-mono-repo-template/{customer-api,schedule-api,api-gateway}:main`
  image tags — **not updated yet**, since that's a live deployment
  concern for `deployment-coolify` once these images are actually being
  built and pushed from the new Dockerfiles, not a source-tree cleanup
  task. Each service's `Dockerfile.dotnet` was renamed to `Dockerfile`
  (replacing the old Node one, which is gone); each service's Node
  `.env.example` was replaced by the .NET one that had been sitting
  alongside it under `src/`, moved up to the service root to match the
  documented folder convention in §3. A short `README.md` was written for
  each service pointing at `dotnet run`/`dotnet build`, replacing the
  stale Node-oriented one.

---

## 7. Database strategy — one schema, two ORMs, temporarily

The single highest-risk part of this migration is the database, because
both Prisma and EF Core will read/write the **same Postgres schema** during
the overlap window (Phases 2–6). Rules for that window:

- **No schema changes originate from the .NET side while any Node service
  still owns a table.** Prisma migrations remain the source of truth until
  a table's owning service has fully cut over.
- EF Core's model for a not-yet-migrated table should be **read-only**
  (`.ToView()` or simply never called for writes) if a .NET service needs
  to read data still owned by a Node service, to avoid two ORMs racing to
  write the same row.
- Once a table's owning service is fully on .NET, ownership of its
  migrations flips to EF Core, and the corresponding Prisma model is
  deleted from `schema.prisma` in the same change that decommissions that
  Node service.
- `created_by`/`modified_by` semantics (`"SYSTEM"` for non-interactive
  writes) must produce byte-identical values from both ORMs during
  overlap — worth a small contract test that inserts via EF Core and reads
  back via Prisma (and vice versa) during Phase 1.

---

## 8. Claude Code tooling — what has to change, deliberately deferred

This repository's `.claude/agents/`, `.claude/rules/`, `.claude/
instructions/`, and `CLAUDE.md` itself are written entirely in terms of the
Node/Fastify/Prisma/AJV stack. They will need a parallel C#/.NET rewrite —
new or updated `backend-service`, `api-builder`, `jwt-security`, `rbac`,
`relational-database`, `database-migrations`, `testing`,
`typescript-standards` (→ a `dotnet-standards` equivalent) — so that
Claude Code sessions on this repo get the same guardrails for C# that they
get for TypeScript today.

That rewrite is **intentionally out of scope for this document** — it's a
distinct, sizeable workstream that should start once Phase 1 (common
packages) has proven out the actual C# conventions in real code, rather
than guessing them up front and having the tooling drift from what got
built. Recommended sequencing: do it once, after Phase 2's `schedule-api`
port has established a real pattern to encode, not before.

---

## 9. Risk register

| Risk | Mitigation |
|---|---|
| Two ORMs racing on the same tables during overlap | Ownership rule in §7; read-only EF Core models for not-yet-owned tables |
| GraphQL federation parity gap (Apollo → HotChocolate/Fusion) | Time-boxed spike before Phase 3 starts; explicit fallback to a REST-only gateway if Fusion parity isn't there |
| JWT/MFA/RBAC subtle behavior drift (highest blast-radius surface) | Port `admin-api` last (Phase 5), after the pattern is proven on the simpler `customer-api` auth surface in Phase 4; a dedicated security pass before cutover, mirroring what the `jwt-security` subagent enforces today |
| Frontend contract drift (response envelope, status codes, field casing) | Contract/integration tests run against **both** the Node and .NET version of a service during shadow mode, asserting byte-for-byte-equivalent JSON shape (case-sensitive field names — watch for C#'s default PascalCase JSON serialization; configure `JsonNamingPolicy.CamelCase` globally to match the existing camelCase wire format) |
| Coolify/Docker build pipeline assumes Node images | New multi-stage .NET Dockerfiles per service (see §10) authored and tested in Phase 2 before any other service follows the pattern |
| Team C# ramp-up time | Phase 2's small/low-risk service doubles as the team's on-ramp; don't start Phase 4/5 until Phase 2 has shipped and soaked |

---

## 10. Docker/Coolify deployment shape

The deploy topology in `docker-compose.yaml` doesn't change — same service
names, same `expose` ports, same healthcheck paths, same env var contract
(`DATABASE_URL`-only, `REDIS_URL`-only, no discrete host/port fallbacks,
per the non-negotiable rules). Only the image build changes, from a
pnpm multi-stage build to a .NET multi-stage build:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY common/ ./common/
COPY apps/backend/admin-api/ ./apps/backend/admin-api/
RUN dotnet restore apps/backend/admin-api/src/AdminApi.csproj
RUN dotnet publish apps/backend/admin-api/src/AdminApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4001
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4001/api/v1/ping || exit 1
ENTRYPOINT ["dotnet", "AdminApi.dll"]
```

The `migrate` service in `docker-compose.yaml` swaps its `command` from
the Prisma `migrate-deploy.sh` script to `dotnet ef database update`
run against the published assembly, once that table's ownership has
flipped to EF Core (see §7) — during overlap, both a Prisma migrate step
and an EF Core migrate step may need to run in sequence.

---

## 11. What this plan does *not* decide yet

These need a short decision (or a spike, as noted) before the phase that
depends on them starts — flagging them now so they don't block Phase 0:

1. **GraphQL gateway approach** (§5.5) — decide before Phase 3.
2. **Hangfire vs. MassTransit** for the queue package — Hangfire is
   recommended as the default (closer to BullMQ's mental model, no new
   broker), but confirm no Node code depends on BullMQ-specific behavior
   (job priorities, flow/dependency graphs) that Hangfire doesn't support.
3. **EF Core snake_case strategy** — naming-convention package vs. explicit
   Fluent API — pick one in Phase 1 and hold it for every entity after.

## 12. Full-scope addendum — CMS and automation are in scope too

Confirmed with the team: this migration is **not** limited to
`apps/backend/*` and `common/*` — `apps/cms` (Strapi) and
`apps/automation` (n8n) are Node.js applications in their own right, and
the end state has no Node.js anywhere except the frontend build toolchain
(§13). Both get replaced, not ported line-by-line (neither Strapi nor n8n
has a direct .NET equivalent — this is a swap of engine, not a code port).

**Good news discovered while scoping this**: as of this migration starting,
both are effectively empty templates in this repo —
`apps/cms/src/api` has **no custom content types defined** (default Strapi
scaffold only, `better-sqlite3`/`pg` deps present but no schema built on
top), and `apps/automation/n8n/workflows` has **no workflows** (just a
`.gitkeep`). That means this is a technology swap in the *template*, not a
data/content migration — there's no real Strapi content or n8n workflow
history to carry over. If that changes before these phases start (someone
builds real content types or workflows in the meantime), re-scope this
section before touching either app.

### 12.1 CMS: Strapi → Piranha CMS

**Piranha CMS** is the recommended replacement over Orchard Core:

| | Piranha CMS | Orchard Core |
|---|---|---|
| Fit with this repo's philosophy | Code-first, EF Core-native, minimal magic — closest to "explicit, not framework-magic" | Module/recipe system, more configuration-over-code, heavier learning curve |
| Hosting model | Runs as a library inside an ASP.NET Core Minimal API app — composes naturally with the rest of this monorepo's services | Runs as its own opinionated host application |
| Database | EF Core + Npgsql, same stack as the rest of the migration | Its own data layer abstraction (YesSql), a second persistence technology to operate |

Plan: new `apps/backend-cms` (or keep `apps/cms` path, swap the
implementation inside it — folder path decision deferred to when this
phase starts) hosting Piranha CMS as an ASP.NET Core app, EF Core against
the same Postgres instance (its own schema/tables, not shared with the
business-domain tables), content types modeled in C# the same "explicit
DTO + service" way as everything else in this plan. Sequence this **after**
Phase 6** (cross-cutting hardening) — it has no dependency on the other
backend services and no urgency while it's still an empty template.

### 12.2 Automation: n8n → Elsa Workflows

**Elsa Workflows** (`Elsa` NuGet packages, v3) is the recommended
replacement — an open-source .NET workflow engine with a visual designer
(Elsa Studio) that's the closest match to n8n's "visual workflow, code
when you need it" model. It runs as an ASP.NET Core host, persists
workflow definitions/instances to Postgres via EF Core, and exposes an
HTTP API + designer UI, mirroring n8n's shape closely enough that the
`apps/automation` folder's purpose (webhook-triggered and scheduled
automations) carries over conceptually unchanged.

Plan: replace the n8n Docker/K8s scaffolding in `apps/automation/n8n/`
(`compose/`, `k8s/`, `scripts/`) with an `apps/automation/elsa/` ASP.NET
Core host following the same deployment shape (Coolify-deployed, its own
Dockerfile). Since there are no existing workflows to port, this is pure
new-build work, not migration work — treat it as its own scoped task once
reached, not something to design in detail now. Sequence after Phase 6,
same as the CMS — neither blocks nor is blocked by the core backend
migration.

### 12.3 Updated phase table

| Phase | Scope |
|---|---|
| 0–7 | Unchanged — `apps/backend/*` and `common/*`, as detailed in §6 |
| **8 — CMS replacement** | Strapi → Piranha CMS, per §12.1 |
| **9 — Automation replacement** | n8n → Elsa Workflows, per §12.2 |

## 13. Frontend scope — confirmed unchanged (as of this plan; superseded, see note below)

`admin-web` (React/Vite), `customer-web` (Next.js), and `customer-mobile`
(Ionic + Capacitor) stay exactly as they are: TypeScript source, React
component model, existing build tooling. Their **build toolchain**
(pnpm, Vite, Next.js's dev/build server) runs on Node.js — that's a
property of how React and Next.js are built, not a choice this migration
can route around, and it's confirmed as acceptable: "no Node.js" means no
Node.js *application* code (backend services, CMS, automation), not "no
Node.js process ever touches this repo." Once Phases 0–9 are complete, the
only Node.js left in the repo is frontend build tooling — no Fastify, no
Strapi, no n8n, no `common/*` TypeScript packages.

> **This section is a historical record of this plan's original scope, not
> the current state.** A second migration (the security-audit-and-Blazor
> plan referenced in `CLAUDE.md`) later moved `admin-web`/`customer-web` to
> Blazor (C#, Phase 4) and `customer-mobile` to .NET MAUI Blazor Hybrid (C#,
> Phase 5) — see `CLAUDE.md`'s "Stack split" section for the current,
> authoritative record. This section is left as-written to preserve what
> this plan actually decided at the time.

## 14. Environment note on this pass

This work is being done in a sandboxed session **without a .NET SDK
available** (`dotnet` is not installed, and the sandbox's network egress
policy blocks `builds.dotnet.microsoft.com`, so it can't be installed
either) — everything is written by hand against known .NET/C# conventions
and cannot be compiled in-session. The developer has confirmed a real
local build on their machine (`dotnet restore && dotnet build` succeeded
for Phase 0 + the first `DotNetMonoRepoTemplate.Types` slice, one warning —
see §15), so the working pattern going forward is: this session writes and
commits a slice, the developer runs `dotnet build` locally in VS Code and
reports back anything that doesn't compile. Treat every commit from this
session as **unverified until that local build confirms it**.

## 15. Progress log

- **Phase 0 (foundation) — done, build-verified locally**:
  `DotNetMonoRepoTemplate.sln`, `Directory.Build.props` (nullable enabled,
  warnings-as-errors on nullable violations, central package management
  on), `Directory.Packages.props`, `.editorconfig` extended with a
  `[*.cs]` section. Target framework corrected from an initial `net8.0` to
  **`net10.0`** partway through — see §17 for why. CI wiring (a
  `dotnet build`/`test`/`format` job alongside the existing Node CI, per
  §6 Phase 0's exit criteria) is still **not done**.
- **Phase 1 (common packages) — in progress**:
  - `common/DotNetMonoRepoTemplate.Types` — **complete**, build-verified
    locally. All of `common/types/src/` ported: `roles.ts` → `RoleName`/
    `TokenScope`, `permissions.ts` → `PermissionName` (renamed from an
    initial `Permission`, which tripped analyzer rule CA1711 — types
    can't end in the reserved suffix `Permission`), `rbac.ts` → `Rbac`,
    `dto/response.dto.ts` → `ResponseDto`, `dto/auth.dto.ts` →
    `AuthDtos.cs`, `events/presence.ts` → `PresenceEvents.cs`,
    `events/signaling.ts` → `SignalingEvents.cs`, `webhook.types.ts` →
    `WebhookTypes.cs`, `batch.types.ts` → `BatchTypes.cs`,
    `reporting.types.ts` → `ReportingTypes.cs`, `upload.types.ts` →
    `UploadTypes.cs`, `disposable-email-domains.ts` →
    `DisposableEmailDomains`. TS string-literal enums (e.g.
    `WebhookEventType.USER_CREATED = "user.created"`) were ported as
    static string-constant classes, not native C# `enum` — a native enum
    would need per-member custom JSON string values, which .NET 8 lacked
    a clean built-in way to do (`[JsonStringEnumMemberName]` only landed
    in .NET 9+); string constants keep the wire format byte-identical
    with zero converter code, consistent with the already-established
    `RoleName`/`TokenScope` pattern. Not yet compiled against .NET 10 —
    should still hold since nothing here is version-sensitive, but flag
    it if `dotnet build` disagrees.
  - `common/DotNetMonoRepoTemplate.Database` — **first pass done, not yet
    build-verified**. `AppDbContext` + entities (`User`, `Role`,
    `UserStatus`, `WebhookSubscription`, `WebhookDelivery`) ported from
    `schema.prisma`, targeting the **same live tables** (no new migration
    — see §7's ownership rule). Snake_case decided as
    **`EFCore.NamingConventions`** (`UseSnakeCaseNamingConvention()`) over
    hand-written Fluent API `.ToTable()`/`.HasColumnName()` calls — less
    boilerplate, one line covers every entity, and it's a well-maintained
    package specifically built for this. Two base classes translate the
    `prisma.md` six-field rule: `AuditableEntity` (all six —
    `User`/`Role`/`UserStatus`/`WebhookSubscription`) and
    `TimestampedEntity` (`Id`/`CreatedAt`/`UpdatedAt` only — the
    `WebhookDelivery` system-mutated exception, matching `prisma.md`'s
    documented case exactly). `CreatedAt`/`UpdatedAt` are stamped by
    `AppDbContext.SaveChanges`/`SaveChangesAsync` overrides on
    Added/Modified — the .NET analogue of Prisma client-side
    `@default(now())`/`@updatedAt`, not a DB trigger. `CreatedBy`/
    `ModifiedBy` are **not** auto-stamped — per the rule, the service
    layer must set them explicitly; the base class only supplies
    `"SYSTEM"` as a property default for the non-interactive-write case.
    A registration extension, `AddDotNetMonoRepoTemplateDatabase(this
    IServiceCollection, connectionString)`, is the .NET analogue of
    `plugins/prisma.plugin.ts`. **Not started**: `DbContext` factory for
    design-time migrations (`dotnet ef migrations add` needs one once a
    table's ownership flips to EF Core), and no migration has been
    generated or run — per the non-negotiable rule, the developer runs
    migrations, and per §7, EF Core doesn't own any table yet regardless.
  - `common/DotNetMonoRepoTemplate.Cache` — **first pass done, not yet
    build-verified**. Ported from `common/cache/src/services/redis.
    service.ts`: `RedisCacheService` (presence, conversation/message/user
    caching, all with the same "cache miss/Redis down → fall back, log a
    warning, never throw" behavior as the Node `safeExecute` wrapper) and
    `AddDotNetMonoRepoTemplateCache(this IServiceCollection, redisUrl,
    tlsRejectUnauthorized)`. Notable divergence from a literal port: the
    Node singleton (`RedisService.getInstance()`, a hand-rolled static
    singleton) becomes a **DI-container-managed singleton**
    (`services.AddSingleton<RedisCacheService>()`) — .NET's built-in
    container already gives you the "instantiate once, share everywhere"
    behavior that pattern exists to fake in Node, so there's no reason to
    duplicate it by hand. `IConnectionMultiplexer` is registered with
    `AbortOnConnectFail = false`, which is StackExchange.Redis's direct
    equivalent of ioredis's retry-then-give-up-without-crashing startup
    behavior. TS's `unknown`/`Record<string, unknown>` cache payloads
    became generic `<T>` methods with `System.Text.Json` — a type-safety
    improvement the original couldn't express, not a behavior change.
    Not ported yet: nothing — this file's full surface is covered.
  - `common/DotNetMonoRepoTemplate.Utilities` — **complete**. Ported
    `crypto.utility.ts`, `date.utility.ts`, `webhook-signature.utility.ts`,
    `api-versioning.utility.ts`. `WebhookSignatureService` (HMAC-SHA256
    webhook signing/verification) is **actively used** in Node
    (`schedule-api`'s webhook processor job, `customer-api`'s webhook
    subscription/delivery services) — ported with care, using
    `CryptographicOperations.FixedTimeEquals` for the signature
    comparison. One deliberate fix: the Node version calls
    `crypto.timingSafeEqual` without a length check first, which **throws**
    on a mismatched-length signature instead of returning `false` — a
    latent bug. The C# port guards the length first and returns `false`,
    which is what every call site actually wants. `CryptoUtil` and
    `ApiVersionManager` were confirmed **unused** anywhere in `apps/` (grep
    came back empty) but ported anyway for completeness; `CryptoUtil`'s
    key derivation was changed from Node's `scryptSync` to
    `Rfc2898DeriveBytes.Pbkdf2` (.NET has no built-in scrypt, and pulling
    in a third-party scrypt package for a class with zero real callers
    wasn't worth the added dependency) — flagging this because it's a
    genuine algorithm change, not just a syntax port, even though nothing
    depends on the exact derivation today.
  - `common/DotNetMonoRepoTemplate.Logging` — **complete**. `Logger` wraps
    Serilog (`Serilog.ForContext` per call-site field, matching Pino's
    "merge this object's keys into the log record" behavior) with the
    same sensitive-key redaction list collapsed to leaf property names
    (the TS list's `req.body.password`-style nested paths are redundant
    with the flat `password` entry for this wrapper's flat key-value
    logging API, so one `HashSet<string>` covers the same ground).
    `IpUtility` ports `ip.utility.ts` (IPv4-mapped-address normalization +
    peppered SHA-256 hash) exactly. `SerilogBootstrapper` replaces
    `createPinoInstance`/`createLoggerOptions`, and
    `TelemetryServiceCollectionExtensions` replaces `opentelemetry.ts`'s
    trace setup via `OpenTelemetry.Extensions.Hosting`, keeping the same
    "only enabled if `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` is set" and
    "ignore /health, /metrics, favicon" behavior. **`otel-log-bridge.ts`
    was not ported** — it exists in Node only because Pino has no native
    OTLP log export, so it hand-parses Pino's JSON stdout stream and
    re-emits it as OTel log records. `Serilog.Sinks.OpenTelemetry` sends
    log events to an OTLP endpoint directly, so the entire bridge file's
    problem doesn't exist in .NET — this is an ecosystem-level
    simplification, not a dropped feature.
  - `common/DotNetMonoRepoTemplate.Email` — **complete**. Ported
    `email.service.ts`'s Mailtrap integration via typed `HttpClient` calls
    to Mailtrap's Send API (`send.api.mailtrap.io`/`sandbox.api.mailtrap.io`
    depending on whether a test-inbox ID is configured) rather than a
    `MailtrapClient` SDK, since Mailtrap has no official .NET SDK — this
    matches the "Mailtrap REST API via HttpClient" option flagged in §2's
    tech mapping table. Both HTML templates (`verify-email.html`,
    `reset-password.html`) copied byte-for-byte into a `Templates/`
    folder, copied to output on build. `{{placeholder}}` substitution
    ported literally, including the same value-to-string rules
    (dates → ISO 8601, primitives → `ToString()`, everything else →
    JSON).
  - `common/DotNetMonoRepoTemplate.Sms` — **complete**. Ported
    `sms.service.ts`'s SMSPortal REST integration (Basic auth, E.164
    phone normalization for the `+27`/`0`-prefixed South African formats
    this template's non-negotiable phone regex already assumes) exactly,
    including the same accept/reject logic on the response body
    (`errors`, `sendResponse.errorReport.faults`, `sendResponse.messages`).
  - `common/DotNetMonoRepoTemplate.Storage` — **mostly complete, one gap
    flagged**. All three providers (`S3StorageProvider`,
    `AzureBlobStorageProvider`, `R2StorageProvider` via AWSSDK.S3's
    S3-compatible client pointed at Cloudflare's endpoint) plus the
    `StorageService` facade that picks one by configured provider, ported
    from `s3-storage.service.ts`/`azure-blob-storage.service.ts`/
    `r2-storage.service.ts`/`storage.service.ts`. **Not ported**:
    `uploadImage()` (the `sharp`-based resize/format-conversion path) —
    porting it needs a .NET image-processing library decision
    (`SixLabors.ImageSharp` is the natural pick, but it's a licensing-
    aware choice — ImageSharp is free for open source/most commercial use
    but has its own commercial license terms above certain thresholds,
    worth a deliberate decision rather than pulled in as a side effect of
    this pass). `validateFileType`/`validateFileSize` ported using ASP.NET
    Core's built-in `FileExtensionContentTypeProvider` instead of the
    `mime-types` npm package — same job, framework-provided instead of a
    third-party dependency.
  - `common/DotNetMonoRepoTemplate.Export` — **mostly complete, one gap
    flagged**. `CsvExporter` (CsvHelper) and `ExcelExporter` (ClosedXML)
    both support buffered export (`ExportToBufferAsync`) and streaming
    a large dataset (`StreamAsync`/`StreamToBufferAsync`), replacing
    `csv-stringify`/`ExcelJS`. Simplified the API surface versus Node:
    dropped `createStream`/`createTransformStream` (Node-stream-specific
    plumbing with no direct .NET equivalent — ASP.NET Core endpoints
    write directly to `HttpResponse.Body`, a `Stream`, which
    `WriteToStreamAsync` already targets) in favor of that one stream-
    based method plus the buffered one. **Flagged gap**: ClosedXML has no
    true streaming XLSX writer the way ExcelJS's
    `stream.xlsx.WorkbookWriter` does — it always builds the workbook in
    memory. `StreamToBufferAsync` for Excel therefore buffers the whole
    `IAsyncEnumerable` into a `List<T>` first, which works but isn't
    memory-streaming for genuinely huge exports. If a huge Excel export
    ever becomes a real requirement, that needs either a different
    library or a CSV fallback — noted here rather than silently accepted.
  - `common/DotNetMonoRepoTemplate.Metrics` — **complete, architecture
    changed deliberately**. `CustomMetricsFactory`/`DatabaseMetrics`/
    `CacheMetrics` ported directly from `custom.collector.ts` (same
    metric names, label names, and bucket boundaries). **`http.collector.ts`
    and `node.collector.ts` were not hand-ported** — `prometheus-net.
    AspNetCore`'s `UseHttpMetrics()`/`MapMetrics()` already provides HTTP
    request counters/histograms out of the box, and `DotNetStats.Register()`
    already provides GC/process/runtime metrics, both natively, both
    better-maintained than a hand-rolled equivalent would be. Likewise,
    **`health.plugin.ts`'s hand-rolled health-check subsystem was replaced
    with ASP.NET Core's built-in `Microsoft.Extensions.Diagnostics.
    HealthChecks`** (`DelegateHealthCheck` + `HealthCheckBuilderExtensions`
    for the database/redis/external-service categories from
    `health-check.builder.ts`, mapping Node's "critical: false" concept
    onto the framework's native per-check `failureStatus`). A
    `HealthResponseWriter` produces the same `{status, timestamp, checks}`
    JSON shape; `service`/`version`/`uptime` fields and the actual
    `/health`, `/health/live`, `/health/ready` route registration are
    left for each service's Phase 2+ `Program.cs`, exactly mirroring how
    the Node fastify plugin is registered per-service today, not in
    `common/`.
  - `common/DotNetMonoRepoTemplate.Observability` — **complete**. Sentry
    config resolution, `SentrySdk.Init` wrapped as a disposable (the
    idiomatic .NET pattern — `using var sentry = SentryBootstrapper.
    Init();` around the whole host, flushing on shutdown), and
    `CaptureException`. 1:1 with `sentry.config.ts`/`sentry.init.ts`/
    `sentry.capture.ts`.
  - `common/DotNetMonoRepoTemplate.Queue` — **complete, simplified by
    necessity, and confirmed unused in Node** (grep for `QueueService`/
    `WorkerService` across `apps/` came back empty, same as `CryptoUtil`).
    BullMQ's model (a named job added to a queue, pulled by a registered
    worker) doesn't map onto Hangfire's model (enqueue a concrete method
    call) without a translation layer, so this port adds one: a static
    `JobHandlerRegistry` keyed by `queueName:jobName` that a generic
    `JobDispatcher.DispatchAsync<T>` (the method Hangfire actually
    enqueues) looks up at execution time — `QueueService<T>.Enqueue`
    replaces `Queue.add`, `WorkerService.RegisterHandler` replaces
    `Worker`'s handler registration. Flagged explicitly: (1) the registry
    is **process-global static state**, a deliberate simplification since
    this codebase runs its background workers colocated with the web
    process rather than as a separate distributed fleet — revisit if that
    deployment shape ever changes; (2) BullMQ's per-job `priority` and
    per-job custom `attempts` don't have a direct per-enqueue-call
    equivalent in Hangfire, whose retry behavior is configured via
    `[AutomaticRetry]` at the job-method or global level — the DTOs
    (`QueueJobOptions.Priority`/`.Attempts`) are ported for shape parity
    but aren't wired to anything yet, since there's no real caller to
    validate the wiring against. Given zero production usage in Node
    today, this whole package should be treated as a **reference
    scaffold**, not a load-bearing port — revisit for real once a Phase 2+
    service actually needs background jobs.
  - **`common/config` was deliberately NOT ported.** It's dead code in
    Node — `grep` for `@node-mono-repo-template/config` across the whole
    repo returns only `common/config/package.json` itself, nothing
    importing it — and what it does is validate env vars with **Zod**,
    directly contradicting the "AJV only, never Zod on the backend"
    non-negotiable rule. Every real service already has its own local
    AJV-schema `EnvConfig` (see `common/cache/src/config/env.config.ts`
    for the pattern this repo actually uses). Porting Zod-validated dead
    code into the new codebase would import a philosophy violation on
    purpose. The .NET equivalent of "validate env vars, fail fast on
    boot" is ASP.NET Core's Options pattern with `.ValidateOnStart()` —
    built into the framework, needs no shared package, and gets wired up
    per-service starting in Phase 2, exactly mirroring how each Node
    service already keeps its own local schema rather than a shared one.
    Worth flagging to the team: `common/config` and its dependency on Zod
    are candidates for deletion from the Node side too, independent of
    this migration.
  - Not started: `dto/auth.dto.ts`'s AJV-equivalent FluentValidation
    validators (validators live per-service, not in `common/`, so these
    come with the first service port in Phase 2/4/5).

**All twelve `common/*` Node packages have now been triaged**: eleven
ported to `DotNetMonoRepoTemplate.*` class libraries (`Types`, `Database`,
`Cache`, `Utilities`, `Logging`, `Email`, `Sms`, `Storage`, `Export`,
`Metrics`, `Observability`, `Queue` — twelve libraries, since `Types` and
`Database` were both already done and `common/config`'s one skip is
offset by `Queue` counting as done-but-flagged), one (`config`)
deliberately not ported for the reasons above. Phase 1 is **functionally
complete** pending the local build-verification loop with the developer —
see §14.

- **Phase 2 (`schedule-api`) — first pass done, not yet build-verified**.
  Full port at `apps/backend/schedule-api/src/*.cs`, sitting alongside the
  still-live Node `*.ts` files at the same path (no filename collisions —
  intentional coexistence per the strangler-fig plan, cleaned up in
  Phase 7). `Program.cs` replaces `application.ts` + `main.ts`; the
  middleware pipeline order (CORS → security headers → rate limiter →
  routing → request logging → API versioning → Swagger (dev only) →
  response-timestamp → API-key auth → endpoints) mirrors the Fastify
  plugin order from `backend.md` as closely as ASP.NET Core's
  routing-then-endpoint model allows. `ApiKeyMiddleware` ports
  `api-key.guard.ts` with `CryptographicOperations.FixedTimeEquals`;
  "public route" detection uses Minimal API's native `.AllowAnonymous()`
  metadata instead of a custom `config.isPublic` flag — same job, using
  the framework's own mechanism instead of reinventing it.
  `CronSchedulerHostedService` (a `BackgroundService` + `PeriodicTimer`)
  replaces `cron-scheduler.service.ts`'s `setInterval`-based job registry
  — .NET's idiomatic "run this repeatedly for the app's lifetime"
  primitive, doing the same job as the hand-rolled Map-of-jobs approach
  without needing to hand-roll it (there's exactly one job today, so the
  registry abstraction wasn't carried over — add it back if a second
  cron job shows up).

  **A real, pre-existing bug was found and fixed during this port, not
  introduced by it**: `webhook-processor.job.ts` locally redefines its
  own `WebhookDeliveryStatus` with **uppercase** values
  (`"PENDING"`/`"DELIVERED"`/`"FAILED"`), shadowing the **lowercase**
  shared enum (`"pending"`/`"delivered"`/`"failed"`/`"retrying"`) from
  `common/types/src/webhook.types.ts` that `customer-api`'s
  `webhook-delivery.service.ts` actually writes to the `status` column.
  Postgres string comparison is case-sensitive, so **schedule-api's cron
  job has never actually matched any row customer-api creates** — the
  query `status: "PENDING"` never finds rows written as `status:
  "pending"`. The .NET port uses the correct shared lowercase convention
  (`DotNetMonoRepoTemplate.Types.WebhookDeliveryStatus`) throughout, so the
  ported version doesn't reproduce the bug — but **the live Node service
  has this bug today**, independent of this migration. Worth a look on
  the Node side regardless of migration timing.

  Two scope-narrowings worth flagging: (1) `RequestLoggingMiddleware`
  ported method/URL/status/duration/correlation-ID logging but **not**
  the full recursive request-body/response-body masking
  (`maskSensitive`) from `request-logger.plugin.ts` — schedule-api has no
  meaningful request bodies to mask (it's a cron/webhook status service),
  so this was a reasonable place to trim scope, but `customer-api` and
  `admin-api` (Phases 4–5) do handle credentials and **must** get the
  full recursive body-masking behavior, not this trimmed version. (2)
  "Helmet" has no direct ASP.NET Core equivalent package in common use;
  `SecurityHeadersMiddleware` hand-rolls the handful of headers Helmet
  sets by default (`X-Content-Type-Options`, `X-Frame-Options`,
  `Strict-Transport-Security`, etc.) rather than pulling in a third-party
  package for it.

  Not yet added: a `.dockerignore`-aware root `docker-compose.yaml` entry
  for the .NET build (still points at the Node `Dockerfile`) — deferred
  to when this service is ready for the shadow-traffic step of §6's
  cutover process, not before local build verification.

- **Phase 3 (`api-gateway`) — GraphQL decision resolved, first pass done,
  not yet build-verified**. §5.5's "spike" turned out to be unnecessary —
  reading the actual code answered it directly: `api-gateway` never ran
  real Apollo Federation/schema stitching despite the package list
  suggesting otherwise. It's one local schema with one resolver set
  (`UserResolvers`) proxying HTTP calls to `customer-api` only, disabled
  by default (`GRAPHQL_ENABLED`). Ported as: **YARP**
  (`Yarp.ReverseProxy`) for the always-on reverse-proxy responsibility
  (`/api`→customer-api, `/admin`→admin-api, `/scheduler`→schedule-api, all
  prefix-preserving — Node's `rewritePrefix` equaling `prefix` meant no
  actual rewriting ever happened, just a mount point), and
  **HotChocolate** for the optional GraphQL layer, matching the real
  single-schema shape rather than the aspirational federated one.

  One deliberate API choice worth flagging: HotChocolate has a
  request-interceptor mechanism (`IHttpRequestInterceptor` +
  `SetGlobalState`) that would be the closer structural match to Apollo
  Server's `context()` function for passing the bearer token into
  resolvers, but its exact method signature varies across HotChocolate
  versions and couldn't be verified against docs in this sandboxed
  session. Used **`IHttpContextAccessor`** injected directly into each
  resolver via `[Service]` instead — a long-stable, well-documented ASP.NET
  Core pattern with much lower risk of a wrong-API-signature compile
  error, at the cost of being slightly less idiomatic HotChocolate.
  Functionally equivalent either way. Since `GRAPHQL_ENABLED` defaults to
  `false` in both the Node original and this port, this is the
  **lowest-confidence piece of this entire migration pass** — give it
  real scrutiny (and a real build) before anyone flips that flag on
  in either environment.

  `RequestLoggingMiddleware` here, unlike `schedule-api`'s, ports the
  **full** recursive `maskSensitive` body-redaction behavior from
  `request-logger.plugin.ts` (`SensitiveDataMasker`, operating on
  `System.Text.Json.Nodes.JsonNode` trees) — api-gateway is the front
  door for real user-facing requests including credentials, so the
  trimmed-down version used for `schedule-api` wasn't appropriate here.
  `HealthService` ports `health.service.ts`'s multi-service aggregation
  (`/health/services`, `/health/services/:name`) as genuinely custom
  logic (real HTTP calls to downstream `/health` endpoints, timing-based
  degraded/healthy classification) alongside the standard
  `/health`/`/health/live`/`/health/ready` trio built on
  `common/metrics`'s health-check extensions, matching
  `application.ts`'s `HealthCheckBuilder` usage for `customer-api`/
  `admin-api`. Preserved verbatim: api-gateway's error envelope is
  `{ error: message }`, **not** the `{ isSuccessful, message }` shape the
  rest of the backend uses — that's an existing inconsistency in the
  Node code, not something this port introduced or should silently
  "fix."

- **Phase 4 (`customer-api`) — first pass done, not yet build-verified.**
  The real auth/JWT surface, ported carefully: `TokenService`
  (`System.IdentityModel.Tokens.Jwt`, HS256, access/refresh pair,
  Redis-backed blacklist + refresh-token store, same TTLs and same
  rotate-on-refresh behavior as the Node original), `AuthGuardMiddleware`
  (bearer extraction → verify → scope check → blacklist check → user
  lookup → optional permission check, registered after `UseRouting()` so
  `AllowAnonymous()` endpoint metadata is resolvable, mirroring
  `auth-guard.plugin.ts`'s `onRequest` hook exactly), and `AuthService`
  (registration with username-character/email-domain/uniqueness checks,
  login with a Redis-backed failed-attempt lockout, email verification,
  neutral-message resend-verification to avoid account enumeration).
  `RequirePermissionsAttribute` ports the RBAC permission-check hook from
  `auth-guard.plugin.ts` even though no current customer-api route
  actually declares required permissions — kept for parity and because
  the mechanism costs nothing to have ready.

  `WebhookSubscriptionService`/`WebhookDeliveryService` port CRUD +
  the delivery/retry/backoff worker logic onto EF Core + `HttpClient`,
  matching `webhook-subscription.service.ts`/`webhook-delivery.service.ts`
  exactly — including using the **correct shared lowercase**
  `WebhookDeliveryStatus` values (`DotNetMonoRepoTemplate.Types`), which is
  what this Node service actually writes (confirmed the case-mismatch bug
  found in Phase 2 is specific to `schedule-api`'s local redefinition, not
  this service).

  **Two scope-narrowings carried over from the `common/*` packages, worth
  restating here since they now affect real endpoints**: (1) `/users/
  export/stream` (`ExportEndpoints`) calls `ExportService.StreamExportAsync`,
  which — per the `common/export` decision — buffers the whole export
  server-side rather than writing chunks progressively to the HTTP
  response the way Node's `reply.raw.write(chunk)` loop does. Functionally
  produces the same file; the actual streaming/memory-efficiency benefit
  Node's version has for very large exports isn't there yet. (2) the
  Excel export path inherits ClosedXML's no-true-streaming limitation
  from `common/export`.

  Also carried over from `common/queue`'s honest assessment: this
  service's `WebhookDeliveryService.ProcessDeliveriesAsync` runs
  synchronously inline (called from `PublishEventAsync`, matching Node's
  `await this.processDeliveries()` after creating each delivery) rather
  than going through the Hangfire scaffold — the Node original doesn't
  use BullMQ here either, so this is a faithful port, not a missed
  opportunity to use the queue package.

  DTOs, FluentValidation validators, and the response envelope
  (`{isSuccessful, data?, message?, errors?}`, with `{isSuccessful:
  false, message: "Validation failed", errors: [{field, message}]}` on
  400s) all match `backend.md`'s documented contract exactly — this is
  the first ported service where that convention is actually exercised
  by real validated endpoints (`schedule-api` and `api-gateway` had
  minimal-to-no user-supplied request bodies to validate).

- **Node source deleted for `schedule-api`, `api-gateway`, `customer-api`**,
  and the five now-unreferenced `common/*` TS packages (`sms`, `storage`,
  `queue`, `config`, `metrics`) removed — see §6.1 for the full record of
  what changed and why this is a deliberate policy change from the
  original "verify before delete" framing in §6's phase table.

- **Phase 5 (`admin-api`) — done, highest-risk service in the migration.**
  Ported the full JWT/RBAC/MFA/audit surface onto ASP.NET Core:

  `TokenService` extends the `customer-api` pattern with everything
  `admin-api`'s Node original had that the customer service didn't:
  `GenerateToken(User, rememberMe, roleName)` takes the full entity
  (matching Node's exact signature) rather than primitive fields;
  `GenerateMfaChallengeToken`/`VerifyMfaChallengeToken` issue and verify
  a 5-minute `type: "mfa_challenge"` JWT signed with the access-token
  key, used between password-success and TOTP-success so a
  password-correct event is never treated as login-complete for an
  MFA-enabled user; `Verify()` now also extracts the `iat` claim and
  returns it on `TokenPayload.Iat`; `InvalidateAllAccessTokensAsync`
  writes a `token:minIat:{userId}` Redis marker (TTL 3600s) and
  `IsSessionInvalidatedAsync` compares a token's `iat` against it —
  the "logout everywhere" pattern required by `jwt-security.md`, so a
  single logout call invalidates every active session for that user,
  not just the token that called it. `LogoutAsync` calls both the
  refresh-token invalidation and the access-token minIat bump.

  `AuthGuardMiddleware` mirrors `customer-api`'s but adds the two
  admin-specific checks from `auth.guard.ts`: `Scope != TokenScope.Admin`
  is rejected, and `IsSessionInvalidatedAsync` is checked before the
  user lookup — both absent from the customer guard since customer-api
  has no MFA/minIat surface.

  `UserService`'s 2FA slice replaces `otplib`+`qrcode` with **Otp.NET**
  (`KeyGeneration.GenerateRandomKey`/`Base32Encoding`/`Totp.VerifyTotp`)
  and **QRCoder** (`PngByteQRCode` → base64 data URL), and replaces
  Node's `crypto.createCipheriv('aes-256-gcm', ...)` TOTP-secret
  encryption with `System.Security.Cryptography.AesGcm`, using the
  same `iv:authTag:ciphertext` hex-joined storage format so existing
  encrypted secrets in the database remain readable across the cutover.

  `AuthService.LoginAsync` keeps the constant-time `DUMMY_HASH` compare
  against `BCrypt.Net.BCrypt.Verify` when no user is found (so login
  timing doesn't leak whether an email exists), the `ADMIN_TIER_ROLES`
  gate on top of password success, and the MFA-challenge branch that
  returns a challenge token instead of real tokens when
  `user.TwoFactorEnabled` — real tokens issue only from
  `VerifyLoginMfaAsync` after `Otp.NET` confirms the code, per
  `jwt-security.md`'s MFA section. `ForgotPasswordAsync` keeps Node's
  neutral "if that email exists" messaging on every non-eligible path
  (no user, wrong tier) to avoid account enumeration.

  `BatchOperationService` ports the generic
  `ExecuteBatch`/`ExecuteBatchWithTransaction` pattern onto
  `AppDbContext.Database.BeginTransactionAsync()` (EF Core's transaction
  API in place of `prisma.$transaction`), with `bulkCreateUsers` using
  the non-transactional path (matching Node — each create commits
  independently) and `bulkUpdateUserStatus`/`bulkDeleteUsers` using the
  transactional path with the same `continueOnError`/`validateBeforeExecute`
  flags per operation.

  `ReportingService` reuses `DotNetMonoRepoTemplate.Export.ExportService`
  (so the same ClosedXML-no-true-streaming and CSV-bom caveats recorded
  for `customer-api` in this log apply here too) and adds the
  `admin-api`-only `GetSystemMetricsReport`/`GetWebhookDeliveryReport`
  queries against the shared `WebhookDeliveries`/`WebhookSubscriptions`
  tables.

  All 26 routes ported 1:1 across `AuthEndpoints`, `UserEndpoints`,
  `BatchEndpoints` (all four gated on `PermissionName.BatchWrite` via
  `RequirePermissionsAttribute`, the first real use of that mechanism
  in the migration — `customer-api` had it wired but unused), and
  `ReportingEndpoints` (`ReportExport` for generate/stream,
  `ReportView` for the three read-only reports), plus the four rate-limit
  tiers from `rate-limit.config.ts` (global 200/min, `auth` 10/min,
  `sensitive` 5/min, `adminOperations` 100/min — the last two new to
  this service).

  **Not invented**: the `/auth/bootstrap-admin` route CLAUDE.md's
  non-negotiable rules describe does not exist anywhere in the current
  Node codebase (confirmed by grep before starting this phase) — this
  is a faithful port of what's actually running, not an opportunity to
  add new functionality mid-migration, so it was not added here either.

  **Lowest-confidence code in this phase**: `Otp.NET`'s
  `VerificationWindow.RfcSpecifiedNetworkDelay` static member (used for
  a ±1-step TOTP tolerance) — referenced from memory of the library's
  public API, not verified against a compiler or the package source in
  this sandbox. If a build ever reports it missing, the fix is a
  one-line swap to `new VerificationWindow(previous: 1, future: 1)`,
  which is unambiguously correct either way.

  **Node source deleted for `admin-api`** (the last Node backend
  service) and, since nothing else consumed them, **all eight remaining
  `common/*` TS packages** (`cache`, `database`, `email`, `export`,
  `logging`, `observability`, `types`, `utilities`) — confirmed via
  repo-wide grep that no `.ts`/`.tsx` file or `package.json` outside
  those packages themselves still referenced
  `@node-mono-repo-template/{cache,database,email,export,logging,
  observability,types,utilities}` before deleting. **The Node backend
  is now fully decommissioned** — `apps/backend/*` contains only .NET
  projects. Root `package.json` scripts `dev:admin-api`/`debug:admin-api`
  removed (their target no longer exists); `dev` now aliases to
  `dev:admin-web`.

  `DotNetMonoRepoTemplate.sln` gained an `AdminApi` entry. While doing
  this, found and fixed a bookkeeping gap from earlier phases: the
  solution file was missing entries for `DotNetMonoRepoTemplate.Sms`,
  `.Storage`, `.Metrics`, and `.Queue` — four C# class libraries that
  were completed in Phase 1 and still exist on disk, but were never
  added to the `.sln`. (These are distinct from the lowercase Node
  packages of similar names — `common/sms`, `common/storage`, etc. —
  which *were* correctly deleted in earlier phases. It's easy to
  conflate the two; worth flagging so a future pass doesn't delete the
  C# libraries by mistake.) All 16 projects now in the `.sln` (12
  `common/*` libraries + 4 services) were verified to resolve to a
  `.csproj` that actually exists on disk.

  **New gap surfaced by this cleanup, flagged for Phase 6**: deleting
  `common/database` removed `prisma/schema.prisma`, which was the only
  versioned, declarative description of the Postgres schema anywhere in
  this repo — `AppDbContext` + the EF Core entity classes describe the
  same tables in C#, but no EF Core migrations have been generated yet
  (per §7, this migration deliberately points EF Core at the schema
  Prisma already created, with no new DDL during the overlap). The live
  database itself is unaffected — this is purely about the repo no
  longer having an in-source-control schema snapshot until Phase 6 runs
  `dotnet ef migrations add InitialCreate` (as a **baseline/no-op**
  migration against the existing schema, not a real DDL change) so the
  schema has a source of truth in git again.

- **Phase 6 (cross-cutting hardening) — in progress**:
  - **Deploy-blocking infra bugs fixed**: `docker-compose.yaml`'s `migrate`
    service ran a Prisma-only script (`devops/scripts/migrate-deploy.sh`)
    against the now-.NET `admin-api` image — it would exit 1 and, via
    `depends_on: service_completed_successfully`, block every backend
    service from ever starting. Turned into a documented no-op (there's
    still no EF Core baseline migration to run in its place — see the gap
    flagged at the end of the Phase 5 entry above, still open). Deleted
    the dead `migrate.sh`/`migrate-deploy.sh` scripts.
  - **Healthcheck fixed**: all four backend Dockerfiles' `HEALTHCHECK`
    used `curl -f`, but `mcr.microsoft.com/dotnet/aspnet:10.0` doesn't
    ship `curl` — added an `apt-get install curl` step to each runtime
    stage, and aligned `docker-compose.yaml`'s own healthchecks from
    `wget -qO-` to `curl -f` to match. Added `DOTNET_ENVIRONMENT:
    Production` to every backend service's compose env block (was
    missing entirely — only `NODE_ENV`, which the .NET config layer also
    reads by that literal env var name for wire-compat, was set).
  - **Self-introduced doc bug fixed**: `schedule-api`'s healthcheck route
    was documented as `/health` in `docker.md`/`deployment-coolify.md`
    (from the earlier `.claude/` tooling rewrite) — the real route,
    confirmed against `HealthEndpoints.cs`, is `/api/v1/ping`.
  - **pnpm workspace cleanup**: `pnpm-workspace.yaml` still listed
    `apps/backend/*` and `common/*` as workspace packages, and
    `allowBuilds` still listed Prisma/bcrypt/protobufjs native deps —
    all dead since those paths have been C# since Phase 5. Removed.
    `turbo.json`'s `globalEnv: ["DATABASE_URL"]` (Prisma-only) removed
    too.
  - **CI rewritten for the actual stack split**: all four
    `.github/workflows/*.yml` files were fully commented out and still
    described the pnpm/Prisma pipeline — uncommented and rewritten.
    `continuous-integration.yml` now runs `dotnet build/test` (backend +
    `common/*`) and `pnpm lint/typecheck/build/test` (frontend) as
    separate parallel jobs feeding one `quality-gate`, with the
    changed-paths filter keyed off `Directory.Build.props`/
    `Directory.Packages.props`/`DotNetMonoRepoTemplate.sln` instead of the
    old shared-TS-package paths; `docker-build`/`deploy` are otherwise
    unchanged (GHCR image build + Coolify webhook). `security-scan.yml`
    gained a NuGet vulnerability job (`dotnet list package --vulnerable`)
    and a `csharp` CodeQL matrix leg alongside the existing
    `javascript-typescript` one. `sonar-project.properties` had its
    backend/`common/*` TypeScript tsconfig and coverage paths removed
    (those sources are C# now, and no coverage report exists for them —
    see below). `version-control.yml` (release-please) was stack-agnostic
    already and just needed uncommenting.
  - **Metrics gap found and fixed**: `DotNetMonoRepoTemplate.Metrics`
    (`AddDotNetMonoRepoTemplateMetrics`/`UseDotNetMonoRepoTemplateMetrics`/
    `MapDotNetMonoRepoTemplateMetrics` — Prometheus `/metrics` endpoint +
    `DatabaseMetrics`/`CacheMetrics`) was only wired into `api-gateway`'s
    `Program.cs`, and even there, only the DI registration and the
    `/metrics` mapping — `UseHttpMetrics()` (via
    `UseDotNetMonoRepoTemplateMetrics()`) was never called anywhere, so no
    service was emitting `http_request_duration_seconds`/request-count
    metrics at all. Wired all three into all four services now, each
    with its own metric-name prefix (`gateway_`/`admin_`/`customer_`/
    `schedule_`) so dashboards don't collide across services — see
    `backend.md`'s middleware pipeline section for the exact placement
    rule. Nothing yet calls `DatabaseMetrics`/`CacheMetrics` from a
    service to record custom per-operation metrics — that DI
    registration exists and is unused, not a bug, just not exercised.
  - **Not yet done**: the EF Core baseline migration (blocks `migrate`
    from being anything more than a no-op — this has been an open gap
    since the Phase 5 entry above and Phase 6 hasn't closed it yet).
    `apps/backend/*/tests` projects now exist for all four services, all
    four now complete (`ScheduleApi.Tests`/`ApiGateway.Tests`/
    `AdminApi.Tests`/`CustomerApi.Tests` — `CustomerApi.Tests`'s
    `WebhookSubscriptionService`/`WebhookDeliveryService` gap, left open
    when the background agent writing them hit the session's usage limit
    mid-task, was closed in a follow-up pass) — SonarCloud
    still has no backend coverage input wired up, since that needs the
    MSBuild-integrated `dotnet-sonarscanner`, not the CLI scanner this
    repo uses (see `sonar-project.properties`'s comment); feature flags
    (judgment call on whether this is in scope — it was never a real
    Node feature in this repo, so there's no "parity" gap to close, only
    a green-field decision to make later if a real use case shows up).
- **Phase 7**: decommission Node backend, completed early — see the
  Phase 5 entry above; folded in per the policy change in §6.1.
- **Phase 8 (CMS: Strapi → Piranha CMS) — done, unverified**: `apps/cms`
  is now an ASP.NET Core (.NET 10) app at `apps/cms/src/Cms.csproj`
  (Microsoft.NET.Sdk.Web), following the same options-pattern/Dockerfile/
  Serilog conventions as the four backend services, but hosting Piranha
  CMS 12.2.0's own Manager admin UI (Razor Pages — a Piranha vendor
  requirement, not a "no MVC" rule violation, since that rule is scoped
  to `apps/backend/*`). Per §12's "good news" callout, the Strapi
  instance it replaced had zero custom content types, zero migrations,
  and zero cross-references from any other app — confirmed again right
  before deleting it — so this was a clean scaffold swap, not a data
  migration; one placeholder `StandardPage` content type (block-based)
  ships so the Manager UI isn't empty on first login. **Materially lower
  confidence than every other phase in this plan**: built without a
  working `dotnet` SDK to compile-check against and without live access
  to piranhacms.org's docs (both blocked in the sandbox that wrote it) —
  verified only against real NuGet package names/versions via
  `api.nuget.org`, not a freshly-fetched reference implementation. The
  `AddPiranha`/`UsePiranha` builder chain in `Program.cs` — specifically
  `options.UseEF<PostgreSqlDb>(...)` and
  `options.UseIdentityWithSeed<IdentityPostgreSQLDb>(...)` — is flagged
  in `apps/cms/README.md` as the most likely spot needing a small fix
  once someone runs `dotnet build` against it for the first time. Wired
  into `docker-compose.yaml` (port 4005, `DATABASE_URL`-only, gated on
  the same `migrate` no-op as the other services), `Directory.Packages.props`,
  and `DotNetMonoRepoTemplate.sln`. `.claude/commands/strapi-setup.md`
  rewritten as a retirement notice rather than deleted, so the
  `/strapi-setup` slash command doesn't dangle.
- **Phase 9 (Automation: n8n → Elsa Workflows) — done, unverified**:
  `apps/automation` is now an ASP.NET Core (.NET 10) Minimal API service
  at `apps/automation/src/WorkflowApi.csproj`, hosting Elsa Workflows
  3.7.1 headless (`Elsa.Workflows.Api`, `Elsa.Http`, `Elsa.Scheduling`,
  persisted to Postgres via `Elsa.Persistence.EFCore.PostgreSql`) — no
  Elsa Studio (the Blazor-based visual designer). That's a deliberate
  architecture decision, not an oversight: this repo's frontend is
  React/Next.js everywhere else, and introducing Blazor as a second UI
  framework to design workflows that don't exist yet wasn't judged worth
  it — `Elsa.Workflows.Api` exposes the same REST surface Elsa Studio
  itself talks to, so a designer can be bolted on later (Elsa Studio or a
  custom React one) without touching this service. Per §12's "good news"
  callout, the n8n setup it replaced (`compose/`, `k8s/`, per-project
  instance provisioning scripts) had zero real workflow definitions
  (`workflows/` was just a `.gitkeep`) and zero cross-references from any
  other app — confirmed again right before deleting it — so, like Phase
  8, this was a clean scaffold swap, not a migration of real automation
  logic. Root `package.json`'s `n8n:local`/`n8n:create-instance`/
  `dev:cms` scripts removed (all three pointed at now-deleted Node
  tooling). **Materially lower confidence than every other phase in this
  plan, same as Phase 8**: built without a working `dotnet` SDK to
  compile-check against and without live access to docs.elsaworkflows.io
  (both blocked in the sandbox that wrote it) — verified only against
  real NuGet package names/versions via `api.nuget.org`. `Program.cs`'s
  `AddElsa`/`UseWorkflowManagement`/`UseWorkflowRuntime`/`UseWorkflowsApi`/
  `UseWorkflows` chain — specifically the exact `UseEntityFrameworkCore`/
  `UsePostgreSql` method names and the ordering of `UseWorkflowsApi()`
  vs. `UseWorkflows()` in the middleware pipeline — is flagged in
  `apps/automation/README.md` as the most likely spot needing a small fix
  on first `dotnet build`. Wired into `docker-compose.yaml` (port 4006,
  `DATABASE_URL` + `CORS_ORIGIN`, gated on the same `migrate` no-op),
  `Directory.Packages.props`, and `DotNetMonoRepoTemplate.sln`.
  `devops/README.md`'s n8n section rewritten to point at the new service.
  **This completes every phase in this plan except one remaining open
  gap: the EF Core baseline migration** (per the non-negotiable rule, the
  developer runs `dotnet ef migrations add`/`dotnet ef database update`,
  not Claude — this genuinely can't be closed from within a session).
  `CustomerApi.Tests`'s two missing service test files were closed in a
  follow-up pass right after this entry.
- **VS Code workspace config — done**: `.vscode/extensions.json` and
  `.vscode/settings.json` updated for a .NET Core workflow alongside the
  existing Node/TS one (both stacks live in this repo simultaneously
  until Phase 7). See §16.

## 16. VS Code workspace configuration for .NET

`.vscode/extensions.json` now recommends, in addition to the existing
Node/TS tooling:

| Extension | Why |
|---|---|
| `ms-dotnettools.csdevkit` | C# Dev Kit — solution explorer, test explorer, project management, debugging (the .NET analogue of having a real IDE, not just a text editor) |
| `ms-dotnettools.csharp` | Base C#/Roslyn language server Dev Kit depends on — listed explicitly since it's also usable standalone |
| `ms-dotnettools.vscode-dotnet-runtime` | Manages the .NET runtime versions other extensions need — avoids "wrong SDK version" friction |
| `csharpier.csharpier-vscode` | Opinionated C# formatter — the CSharpier-for-C# analogue of Prettier-for-TS already in this repo, keeps formatting a non-debate the same way Prettier does |
| `josefpihrt-vscode.roslynator` | Extra Roslyn analyzers/refactorings — the C# analogue of the ESLint plugin set already recommended (`eslint-plugin-sonarjs`, `eslint-plugin-unicorn`) |
| `jmrog.vscode-nuget-package-manager` | NuGet package management UI — the .NET analogue of having pnpm-aware tooling in-editor |
| `ckolkman.vscode-postgres` | Inline Postgres client — useful once EF Core owns tables directly, same DB this repo already runs |
| `editorconfig.editorconfig` | Makes VS Code's native editor respect the `.editorconfig` `[*.cs]` section added in Phase 0, not just the C# extension's own formatting |

`.vscode/settings.json` additions:

- `dotnet.defaultSolution: "DotNetMonoRepoTemplate.sln"` — points Dev Kit at
  the solution so it doesn't have to guess or prompt.
- `[csharp]` language override: `csharpier.csharpier-vscode` as the
  formatter, `tabSize: 4`, `rulers: [120]` — C# convention, distinct from
  the TS side's 2-space/140-column settings already in this file.
- `csharp.format.enable: false` — turns off the C# extension's own
  formatter so it doesn't fight CSharpier, mirroring how `prettier.
  requireConfig`/ESLint are kept from double-formatting TS today.
- `omnisharp.enableEditorConfigSupport` / `enableRoslynAnalyzers` — same
  "respect the shared config, surface analyzer warnings inline" posture
  already applied to the TS side via `eslint.validate` and SonarLint.
- `files.exclude` / `files.watcherExclude` / `search.exclude` extended
  with `**/bin`, `**/obj`, `**/.vs` — the .NET equivalents of the
  already-excluded `node_modules`/`dist`/`.turbo`.
- `cSpell.words` extended with the .NET-stack terms now appearing in the
  repo (`dotnet`, `efcore`, `npgsql`, `hangfire`, `piranha`, `serilog`,
  `fluentvalidation`, etc.) so the spell checker stops flagging them.

Nothing Node/TS-facing was removed — both toolchains are recommended
side by side for the duration of the migration, exactly as both stacks
coexist in the repo until Phase 7 decommissions the Node backend.

## 17. Target framework correction: .NET 10, not .NET 8

The original scaffold (§1's first pass) targeted `net8.0`, on the
reasoning that .NET 8 was "the current LTS." Checked directly against
Microsoft's support policy while building out Phase 1 — that's stale:

| Version | Type | Released | Support ends |
|---|---|---|---|
| **.NET 10** | **LTS** | Nov 11, 2025 | Nov 14, 2028 |
| .NET 9 | STS | Nov 12, 2024 | Nov 10, 2026 |
| .NET 8 | LTS | Nov 14, 2023 | Nov 10, 2026 (in Maintenance) |

.NET 10 is the current LTS, not .NET 8 — .NET 8 is now in its
maintenance window with about three months of support left as of this
plan. Starting a multi-month, multi-phase migration on a framework
version that goes out of support before the migration finishes would be
a mistake to build in on purpose. `Directory.Build.props` was corrected
to `net10.0` before any service code was written on top of it — only the
foundation and the first `common/*` library existed at that point, so the
correction cost nothing. If you're reading this after Phase 2 or later
has shipped, the target framework should already be `net10.0` throughout;
if you find `net8.0`/`net9.0` anywhere in the solution at that point,
that's a bug, not a deliberate per-project choice.

## 18. Confirmed package versions (as of this research pass)

Pulled directly from the NuGet API rather than assumed, so the first pass
at each package doesn't need a version bump before it even compiles.
Re-check before use if picking this back up much later — these move fast:

| Package | Version | Used by |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | `common/database` — done |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.10 | `common/database` — done |
| `EFCore.NamingConventions` | 10.0.1 | `common/database` — done |
| `FluentValidation` | 12.1.1 | first service port (Phase 2) |
| `FluentValidation.AspNetCore` | 11.3.1 | first service port — note this one hasn't reached a v12 release matching core `FluentValidation` yet, confirm compatibility when it's actually wired in |
| `System.IdentityModel.Tokens.Jwt` | 8.22.0 | `TokenService` port (Phase 4/5) |
| `BCrypt.Net-Next` | 4.2.1 | `TokenService`/`UserService` port |
| `Otp.NET` | 1.4.1 | 2FA port (Phase 5, `admin-api`) |
| `StackExchange.Redis` | 3.1.3 | `common/cache` |
| `Hangfire.Core` + `Hangfire.Redis.StackExchange` | 1.8.24 / 1.12.0 | `common/queue` |
| `Serilog.AspNetCore` | 10.0.0 | `common/logging` |
| `OpenTelemetry.Extensions.Hosting` | 1.17.0 | `common/observability` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.17.0 | `common/observability` |
| `Sentry.AspNetCore` | 6.8.0 | `common/observability` |
| `MailKit` | 4.17.0 | `common/email` |
| `AWSSDK.S3` | 4.0.101.7 | `common/storage` |
| `Azure.Storage.Blobs` | 12.29.1 | `common/storage` |
| `ClosedXML` | 0.105.1 | `common/export` |
| `CsvHelper` | 33.1.0 | `common/export` |
| `Swashbuckle.AspNetCore` | 10.2.3 | per-service API docs |

Only the first three are actually in `Directory.Packages.props` right
now, per that file's own rule ("don't add a version nothing references
yet") — the rest are recorded here so the next phase doesn't have to
re-research them.

---

## Summary

This is a **strangler-fig migration**, not a rewrite-and-flip. The
sequencing is: foundation → shared libraries → smallest service →
gateway → customer-facing service → highest-risk admin/auth service →
cross-cutting hardening → decommission. Every non-negotiable rule in
`CLAUDE.md` has a named C#/.NET equivalent, and every subsystem (JWT,
RBAC, audit log, webhooks, feature flags) ports as hand-rolled service-
layer code, matching today's philosophy of "explicit, not framework-
magic." The Claude Code tooling (`.claude/agents`, `rules`, `CLAUDE.md`)
gets its own C#-flavored rewrite once real patterns exist to encode,
deliberately sequenced after Phase 2 rather than guessed up front.
