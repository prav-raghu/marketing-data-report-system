# Request / Response Logging

All backend services use a shared `RequestLoggingMiddleware` for request and response logging. Endpoint delegates never log their own request/response data — the middleware handles it at the pipeline level. See the `backend-service` agent for the middleware pipeline order this fits into.

## How It Works

`Middleware/RequestLoggingMiddleware.cs` (identical shape in every service, just a different namespace) wraps the rest of the pipeline:

```
Request arrives
     │
     ▼
Log "Incoming request"    → method, url, x-correlation-id (info level)
     │
     ▼
  (non-GET with a body)
     │
     ▼
Log "Request body"        → masked request body (debug level)
     │
     ▼
  (buffer the response, call _next(context))
     │
     ▼
  (endpoint delegate runs)
     │
     ▼
Log "Outgoing response"   → statusCode, durationMs, masked response body (info level)
     │
     ▼
Response sent
```

The middleware buffers the response body into a `MemoryStream` to read and log it, then copies it back to the real response stream — this is why `RequestLoggingMiddleware` must run early in the pipeline (see `rules/backend.md`'s ordering), so every downstream middleware's output is captured.

## Sensitive Field Masking

Before any body or response object is written to the log, it passes through `SensitiveDataMasker.TryParseAndMask()`. This walks the parsed `JsonNode` tree and replaces the value of every sensitive key with `[REDACTED]`.

Fields that are always masked (see `Middleware/SensitiveDataMasker.cs`'s `SensitiveKeys` set — `admin-api`'s version additionally masks `mfaToken`/`authToken`/`code`, since those are real fields on its auth surface that `customer-api`/`schedule-api` don't have):

| Category | Fields |
|---|---|
| Passwords | `password`, `currentPassword`, `newPassword`, `confirmPassword` |
| Tokens | `token`, `accessToken`, `refreshToken`, `authToken` (admin-api), `mfaToken` (admin-api) |
| Secrets | `secret`, `apiKey`, `api_key`, `clientSecret`, `privateKey` |
| Payment | `creditCard`, `cardNumber`, `cvv`, `cvc` |
| Identity | `ssn`, `nationalId` |
| MFA | `pin`, `otp`, `twoFactorCode`, `code` (admin-api) |
| HTTP | `authorization`, `cookie` |

Masking is recursive — nested objects and arrays are walked fully. The original request/response payload is never modified; masking only applies to the copy written to the log.

### Example

Input body:

```json
{
  "email": "user@example.com",
  "password": "s3cr3t!",
  "profile": {
    "name": "Alice",
    "pin": "1234"
  }
}
```

What appears in the log:

```json
{
  "email": "user@example.com",
  "password": "[REDACTED]",
  "profile": {
    "name": "Alice",
    "pin": "[REDACTED]"
  }
}
```

## Log Levels

| Point | Level | Reason |
|---|---|---|
| Incoming request | `Info` | Always visible in production |
| Request body | `Debug` | Suppressed in production by default (`LOG_LEVEL=info`) |
| Outgoing response | `Info` | Always visible in production |

To enable body logging in development, set `LOG_LEVEL=debug` in the service `.env`.

## Skipped Routes

The following are never logged to reduce noise (`ShouldSkip` in `RequestLoggingMiddleware.cs`):

- `OPTIONS` — CORS preflight requests
- `/ping`/`/ready` (or `/health`, depending on the service) — liveness/readiness probes
- `/docs*` — Swagger UI assets

## Correlation IDs

When a request carries an `x-correlation-id` header, it is included in both the incoming and outgoing log entries. This allows tracing a single request across multiple log lines and across distributed services.

Upstream callers (API gateway, frontend) should set this header on every request.

## Sample Log Output

```json
{ "level": "info", "name": "RequestLogger", "msg": "Incoming request", "method": "POST", "url": "/api/v1/auth/login", "correlationId": "abc-123" }
{ "level": "debug", "name": "RequestLogger", "msg": "Request body", "email": "admin@example.com", "password": "[REDACTED]", "rememberMe": true }
{ "level": "info", "name": "RequestLogger", "msg": "Outgoing response", "method": "POST", "url": "/api/v1/auth/login", "statusCode": 200, "durationMs": 47, "correlationId": "abc-123", "response": { "isSuccessful": true, "data": { "authToken": "[REDACTED]", "refreshToken": "[REDACTED]", "username": "admin" } } }
```

## Adding a New Service

Copy `RequestLoggingMiddleware.cs` and `SensitiveDataMasker.cs` into the new service's `Middleware/` folder (update the namespace), then register in `Program.cs` in the correct pipeline position — after `UseRateLimiter()`, before `ApiVersionMiddleware`:

```csharp
app.UseMiddleware<RequestLoggingMiddleware>();
```

That is all. No changes to endpoint delegates are required.

## Keeping Sensitive Keys in Sync

`SensitiveDataMasker.SensitiveKeys` and `DotNetMonoRepoTemplate.Logging.Logger`'s own redaction cover the same fields via different mechanisms:

- `Logger`'s internal redaction — applied to structured log objects the `Logger` wrapper writes via Serilog
- `SensitiveDataMasker.SensitiveKeys` — applied manually by the middleware when it serializes request/response JSON before handing it to the logger

When a new sensitive field is added to any service's DTOs, update **both** locations, and check whether the new field name needs adding to services beyond the one it was found in — the sets aren't currently shared/centralized (each service's `Middleware/SensitiveDataMasker.cs` has its own copy of the list), so a field added to `admin-api`'s list doesn't automatically apply to `customer-api`.
