---
applyTo: "apps/backend/**/Program.cs,apps/backend/**/Endpoints/**"
description: "OpenAPI/Swagger auto-generation via Swashbuckle.AspNetCore"
---

Every backend service exposes Swagger UI at `/docs` in non-production environments. Swashbuckle reads endpoint metadata (route, parameter types, response types) automatically from the Minimal API registrations — there's no separate AJV-schema-to-Swagger translation step the way the Node era needed, since the DTOs themselves are the source of truth.

## Required Packages

Already in `Directory.Packages.props` (central package management — never add a version inline in a service's `.csproj`):

```xml
<PackageVersion Include="Swashbuckle.AspNetCore" Version="..." />
```

Reference it in the service's `.csproj`:

```xml
<PackageReference Include="Swashbuckle.AspNetCore" />
```

## Registration (`Program.cs`)

Every service registers Swagger the same way — see any current `Program.cs` for the exact, already-working version:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Customer API", Version = "1.0.0" });
    options.AddSecurityDefinition("BearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
});
```

Then, after building the app but before mapping endpoints:

```csharp
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "docs");
}
```

`app.Environment.IsProduction()` reads `DOTNET_ENVIRONMENT` — this is the .NET equivalent of the Node era's `NODE_ENV !== 'production'` check, and it's what actually gates `/docs` now (not a manual `NODE_ENV` string comparison).

## DTOs are the schema — no separate annotation step required for basic docs

Because every request/response DTO is already a `sealed record` with `required`/nullable properties correctly expressing which fields are mandatory, Swashbuckle infers a reasonably complete schema automatically. Two things still worth adding explicitly:

- **Tags/grouping**: Swashbuckle groups by the endpoint's route prefix by default (`/api/v1/products` → "Products" isn't automatic — check the actual grouping behavior in a running service before assuming parity with the old `tags: ['Products']` AJV convention; if grouping needs to be explicit, use `.WithTags("Products")` on the endpoint registration)
- **Operation summaries**: `.WithSummary("...")`/`.WithDescription("...")` on an endpoint registration, where the route name alone isn't self-explanatory

```csharp
group.MapGet("/{id}", async (string id, ProductService service) => { /* ... */ })
    .WithTags("Products")
    .WithSummary("Get a product by ID");
```

## Security on Endpoints

Endpoints marked `.AllowAnonymous()` need no extra Swagger configuration — Swashbuckle's `AddSecurityDefinition`/global security requirement (if configured) applies to authenticated routes by default; `.AllowAnonymous()` is itself the signal, no separate `security: []` override needed the way the AJV-schema era required one per-route.

## Accessing Docs

| Environment | URL |
|---|---|
| Local dev | `http://localhost:{PORT}/docs` |
| Staging | `http://staging-host:{PORT}/docs` |
| Production | **not exposed** (`DOTNET_ENVIRONMENT=Production` disables `/docs`) |

## Swagger UI Per Service

| Service | Port | Docs URL |
|---|---|---|
| api-gateway | 4000 | `http://localhost:4000/docs` |
| admin-api | 4001 | `http://localhost:4001/docs` |
| customer-api | 4002 | `http://localhost:4002/docs` |
| schedule-api | 4003 | `http://localhost:4003/docs` |

## Rules

- NEVER expose `/docs` in production — controlled via `!app.Environment.IsProduction()`
- Prefer letting Swashbuckle infer schema from DTOs over hand-writing OpenAPI annotations — only add `.WithTags`/`.WithSummary` where the default output is genuinely unclear
- `AddSwaggerGen`/`AddEndpointsApiExplorer` must be registered before `builder.Build()`; `UseSwagger`/`UseSwaggerUI` must be called on the built `app`, in the position shown in any current `Program.cs` (after the middleware pipeline setup, before endpoint mapping — see `rules/backend.md`'s authoritative pipeline order)
