# API Versioning Strategy

## Overview

This monorepo implements API versioning across all backend services (`customer-api`, `admin-api`, `schedule-api`) via `Middleware/ApiVersionMiddleware.cs` — one copy per service, identical shape, different namespace. **This document previously described a more elaborate versioning system (deprecation `Sunset` headers, an `ApiVersionManager` utility class, per-version deprecation dates) than what's actually implemented, in both the Node era and the .NET port** — the sections below are corrected to describe the real, current middleware, with the aspirational parts clearly marked as not built.

## Version Detection Methods (as actually implemented)

The API version is detected in this order of precedence, in `ApiVersionMiddleware.InvokeAsync`:

### 1. Custom Header
```bash
curl -H "api-version: v2" https://api.example.com/users
```

### 2. Accept Header
```bash
curl -H "Accept: application/vnd.api.v2+json" https://api.example.com/users
```

### 3. URL Path
```bash
curl https://api.example.com/api/v2/users
```

Falls back to the current default (`"v1"`) if none of the three match. See `rules/backend.md` for the exact regex patterns used (`AcceptVersionRegex`/`UrlVersionRegex`).

## Response Headers

Every response includes:

```
X-API-Version: v1
```

**Deprecation headers (`Deprecation`, `Sunset`, `X-API-Deprecation-Info`) are not implemented.** No service currently marks a version as deprecated or sets a sunset date — `SupportedVersions` in `ApiVersionMiddleware.cs` is a flat array (`{ "v1", "v2" }`) with no per-version metadata. If a task needs deprecation signaling, that's new functionality to design, not something to assume already exists.

## Current Version Status

`v1` and `v2` are both accepted by `ApiVersionMiddleware` in every service, but in practice only `v1` routes are actually registered anywhere (`admin-api`'s `V2Routes`-equivalent, where it exists, is a stub health-check-only registration — see `webhook-events.instructions.md`/`backend-service.md` for what's real per service before assuming `v2` has meaningful content).

## Usage Examples

### Frontend Integration

```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'api-version': 'v1',
  },
});
```

There is no deprecation-warning interceptor to write against real response headers, since the backend doesn't send any (see above) — don't add one that reads headers the backend never sets.

### URL-Based Versioning

```typescript
const response = await fetch('/api/v1/users');
```

### Backend Service URLs

- **Customer API**: `http://localhost:4002/api/v1/`
- **Admin API**: `http://localhost:4001/api/v1/`
- **Schedule API**: `http://localhost:4003/api/v1/`

## Version Management

There is no `ApiVersionManager` utility class in this codebase (the Node era's `common/utilities/src/apiVersioning.ts` was never load-bearing beyond the plugin itself, and its .NET port — `DotNetMonoRepoTemplate.Utilities` — doesn't have an equivalent; check before assuming one exists). Version support is declared directly as a `static readonly string[]` inside each service's `ApiVersionMiddleware.cs`.

### Adding a New Version

1. **Update `SupportedVersions`** in the service's `Middleware/ApiVersionMiddleware.cs`:

```csharp
private static readonly string[] SupportedVersions = { "v1", "v2", "v3" };
```

2. **Create the new endpoints** — this codebase doesn't use a per-version routes directory the way the Node era's `routes/v3/` did; a new version's endpoints are just new `Endpoints/*.cs` files mapped under a `/api/v3` route group in `Program.cs`:

```csharp
var v3Group = app.MapGroup("/api/v3/products");
```

3. **Map the new group** in `Program.cs` alongside the existing `app.Map<Domain>Endpoints()` calls.

## Error Handling

### Unsupported Version

```json
{
  "success": false,
  "error": "Unsupported API version",
  "supportedVersions": ["v1", "v2"]
}
```

Status: `400 Bad Request` — see `ApiVersionMiddleware.InvokeAsync` for the exact response shape (note: `success`, not `isSuccessful` — this endpoint's error shape doesn't follow the standard `ResponseDto` envelope, a pre-existing inconsistency carried over from the Node original, not something to "fix" without being asked).

## Best Practices

1. **Never break `v1` routes** — keep backward compatibility
2. **Gradual migration** — give clients real notice before deprecating anything (once deprecation signaling actually exists — see above)
3. **Document changes** — maintain a changelog for each version
4. **Use semantic versioning** — major version for breaking changes

## Testing

```bash
# Test header-based
curl -H "api-version: v2" http://localhost:4002/api/v1/ping

# Test URL-based
curl http://localhost:4002/api/v2/ping

# Test invalid version
curl -H "api-version: v99" http://localhost:4002/api/v1/ping
```

## Configuration

Versioning configuration is per-service in:

```
apps/backend/<service>/src/Middleware/ApiVersionMiddleware.cs
```

## Architecture

```
Request → ApiVersionMiddleware → Version Detection → Set X-API-Version header
                                        ↓
                            400 if unsupported, otherwise continue
                                        ↓
                            Downstream middleware / endpoint
```

Registered after `RequestLoggingMiddleware`, before Swagger — see `rules/backend.md` for the authoritative pipeline order.
