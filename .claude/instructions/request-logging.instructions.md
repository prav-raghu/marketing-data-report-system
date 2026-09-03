# Request / Response Logging — Implementation Instructions

Every ASP.NET Core backend service in this template includes a `Middleware/RequestLoggingMiddleware.cs` that logs all inbound requests and outbound responses through the shared `DotNetMonoRepoTemplate.Logging.Logger` class, which is Serilog-backed and routes to OpenObserve via OTLP HTTP (configured by `DotNetMonoRepoTemplate.Observability`'s `AddDotNetMonoRepoTemplateTelemetry`). This replaces the Node era's `request-logger.plugin.ts`/Pino setup — same behavior, different implementation.

---

## Middleware location

```
apps/backend/<service>/src/Middleware/RequestLoggingMiddleware.cs
```

The middleware is nearly identical across services (only the namespace and the skipped-route list differ slightly per service's health-route naming — see `rules/backend.md`) — copy it verbatim when adding a new backend service.

---

## What it logs

| Point | Logged fields | Skipped when |
|---|---|---|
| Incoming request | `method`, `url`, `correlationId` | `OPTIONS`, `/ping`, `/ready` (or `/health`, per service), `/docs` |
| Request body (non-GET only) | masked request body, `Debug` level | `OPTIONS`, `GET`, skipped routes |
| Outgoing response | `method`, `url`, `statusCode`, `durationMs`, `correlationId`, masked response body | `OPTIONS`, skipped routes |

Sensitive keys in both request body and response body are replaced with `[REDACTED]` before logging, via `Middleware/SensitiveDataMasker.cs`'s `SensitiveKeys` set (`password`, `token`, `accessToken`, `refreshToken`, `apiKey`, `cvv`, `ssn`, `otp`, etc. — `admin-api`'s copy additionally masks `mfaToken`/`authToken`/`code`).

`durationMs` is the wall-clock time from request start to response completion, measured with `DateTime.UtcNow` deltas inside the middleware. Use this field in OpenObserve to identify slow endpoints.

---

## Registration

Register in `Program.cs` **after** `app.UseRateLimiter()` and **before** `ApiVersionMiddleware`:

```csharp
app.UseRateLimiter();
app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();   // ← here
app.UseMiddleware<ApiVersionMiddleware>();
```

See `rules/backend.md` for the full, authoritative middleware pipeline order — don't reorder relative to what's documented there.

---

## No type augmentation needed

Unlike the Fastify era (which needed a `requestStartTime` field added to `FastifyRequest` via a `.d.ts` declaration merge), ASP.NET Core's `HttpContext` doesn't need extending — the middleware just uses a local variable for the start timestamp, scoped to its own `InvokeAsync` call.

---

## Correlation IDs

Pass `X-Correlation-Id` as a request header to tie a distributed request chain together in OpenObserve. The middleware reads this header on both the incoming and outgoing log entries and includes it in every log line.

Clients (API gateway, other services) should generate a GUID and forward it on all downstream calls:

```csharp
request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
```

Query correlated logs in OpenObserve:

```sql
SELECT * FROM "default" WHERE correlationId = 'your-uuid-here'
```

---

## Querying request logs in OpenObserve

```sql
-- All requests to a specific route
SELECT * FROM "default" WHERE url LIKE '/api/v1/webhooks%'

-- All 4xx/5xx errors
SELECT * FROM "default" WHERE statusCode >= 400

-- Slow responses (> 500 ms)
SELECT * FROM "default" WHERE durationMs > 500

-- Requests from a specific correlation chain
SELECT * FROM "default" WHERE correlationId = 'abc-123'
```

---

## Do not log

- Raw IP addresses — hash before logging if a service ever needs to log them (no `PEPPER`-based hashing utility has been ported for this specifically; check `DotNetMonoRepoTemplate.Utilities` before assuming one exists)
- Auth headers — stripped automatically by `SensitiveDataMasker`'s key list
- Health/readiness endpoints — filtered to avoid noise
- OPTIONS preflight requests — filtered to avoid noise

---

## Sentry runs independently of Serilog/OpenObserve

Sentry (`DotNetMonoRepoTemplate.Observability`'s `SentryBootstrapper`/`SentryCapture`) captures exceptions separately from this request/response logging. In the central `AppExceptionHandler`, both run for an unhandled exception: `Logger.Error(...)` logs it to OpenObserve via Serilog's OTLP sink, and `SentryCapture.CaptureException(exception)` sends it to Sentry. They are two distinct sinks for two distinct purposes — structured request/response telemetry in OpenObserve, exception aggregation and alerting in Sentry. Do not collapse them into a single call or assume one replaces the other. Sentry is a no-op when `SENTRY_DSN` is unset, so local dev still logs to OpenObserve only.

Note: Serilog's OTLP sink eliminates the need for the Node era's hand-rolled `otel-log-bridge.ts` — Serilog ships to OpenObserve natively, no separate bridge layer.

---

## OpenObserve setup

See `documentation/dotnet-migration-plan.md` for the Serilog/OpenObserve OTLP configuration this replaced from the Node era. `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`/`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`/`OTEL_EXPORTER_OTLP_HEADERS` env vars are read the same way (see any service's `.env.example`) — the OpenObserve Docker Compose setup itself (`devops/docker-compose.dev.yml`) is unchanged by the backend migration.
