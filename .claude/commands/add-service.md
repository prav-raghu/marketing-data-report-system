---
description: Add a complete new backend service to the monorepo with all boilerplate
argument-hint: <service name and purpose, e.g. "notification-api for push notifications and email alerts">
---

Scaffold a new backend service: $ARGUMENTS

Use the `new-service-scaffold` subagent and match `customer-api`'s structure exactly — do not introduce new patterns. Before writing any code, read the existing `customer-api` to understand its current middleware pipeline order, options pattern, and endpoint registration.

1. Create the full directory structure under `apps/backend/{service-name}/src/`
2. Copy and adapt patterns from `customer-api`: `Program.cs`, `<Service>.csproj`, `appsettings.json`, `Configuration/<Service>Options.cs` + `Validator.cs` + `Factory.cs`, `Auth/` (AuthGuardMiddleware, CurrentUser, RequirePermissionsAttribute — unless the service uses a different auth mechanism, like `schedule-api`'s API-key auth), `Middleware/` (copy verbatim, just change the namespace), `Endpoints/`
3. Assign the next available port (check existing: 4000, 4001, 4002, 4003)
4. Create a `Dockerfile` at `apps/backend/{service-name}/Dockerfile` using the multi-stage .NET template from `rules/docker.md`
5. Add the new project to `DotNetMonoRepoTemplate.sln` (new GUID, `Project(...)` entry + its four `ProjectConfigurationPlatforms` lines)
6. Add to root `docker-compose.yaml`
7. Add a YARP route in `api-gateway` if the service should be reachable through the gateway
8. Create `.env.example`
9. Run `dotnet build DotNetMonoRepoTemplate.sln` — zero errors, zero warnings required
