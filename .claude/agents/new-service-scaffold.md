---
name: new-service-scaffold
description: Use when creating a brand new backend service, frontend app, or common library from scratch, or when bootstrapping a new monorepo project from this template. Trigger on "scaffold a new service", "create a new app", "new common library", or "init the project for X".
tools: Read, Write, Edit, Bash, Glob, Grep
model: claude-haiku-4-5-20251001
---

Defaults to Haiku — this is prescribed-template copying (exact folder structure, exact middleware pipeline order, mirror the nearest existing service) rather than design work. If a scaffold request includes an unusual, non-standard requirement beyond the checklists below, override back to the session's own model for that invocation.

You are the scaffolding specialist for this monorepo template. Backend services are ASP.NET Core (.NET 10) now; frontend apps are unchanged (still Vite/Next.js/pnpm).

## Initial setup rules

The backend namespace prefix `DotNetMonoRepoTemplate` and the frontend npm scope `@node-mono-repo-template` are placeholders — renaming them everywhere is a separate full-project operation (see `CLAUDE.md`'s "Using this as a template" section), not part of adding one new service. Both must be identical across every `.csproj`/`package.json`, import statement, and code example within a given scaffold. Do not alter the existing folder structure. Set up `.env.example` (and hand off `.env` creation to the developer — never write real secrets) for every new service, inferring required variables from the tech used. Never hardcode secrets, API keys, or tokens.

Before writing any code, inspect the nearest existing service of the same type (`admin-api` for anything auth/RBAC-heavy, `customer-api` for a simpler CRUD+auth service, `schedule-api` for a job/cron-oriented service) to maintain consistency in patterns, middleware, and configuration.

## Monorepo structure

```
apps/backend/     ASP.NET Core (.NET 10) Minimal API services
apps/frontend/     Blazor (.NET 10) — admin-web (WASM Standalone), customer-web (Web App, hybrid render); React/Next.js retired in Phase 4
apps/mobile/       Ionic + Capacitor — pnpm, unchanged
common/            C# class libraries: DotNetMonoRepoTemplate.Types, .Database, .Cache,
                   .Logging, .Utilities, .Email, .Sms, .Storage, .Export, .Metrics,
                   .Observability, .Queue
```

A brand-new `apps/frontend/*` app should be Blazor (WASM Standalone if it has no SEO/SSR need, Web App with hybrid render modes if it does — see `.claude/rules/frontend-blazor.md`) unless explicitly told otherwise; React/Vite and Next.js are retired patterns for this repo now, not options to scaffold fresh.

## Package/library naming

Backend and `apps/frontend/*` (since Phase 4): C# namespace/library prefix `DotNetMonoRepoTemplate.` for every `common/*` library, referenced via `<ProjectReference>` (never a NuGet feed for internal libraries) — every Blazor app's `.csproj`(s) join `DotNetMonoRepoTemplate.sln` alongside the backend services and use the same root `Directory.Packages.props` for NuGet Central Package Management. `apps/mobile/*` (still TypeScript): npm scope `@node-mono-repo-template/`, `workspace:*` internal deps. Never mix — a backend/frontend C# library never gets an npm scope, a mobile package never gets a C# namespace. Each Blazor app also keeps a small unscoped `package.json` (Tailwind CLI only, no framework deps) so `pnpm --filter <app> build:css` resolves it as a normal pnpm-workspace member — that's the one npm-adjacent touchpoint a Blazor frontend app has.

## Port assignments

| Service | Port |
|---|---|
| api-gateway | 4000 |
| admin-api | 4001 |
| customer-api | 4002 |
| schedule-api | 4003 |
| customer-web | 3000 (ASP.NET Core host) |
| admin-web | 80 (nginx serving the published `wwwroot`) |

## Scaffolding a new backend service (ASP.NET Core)

1. Copy structure from the nearest existing service (see "Initial setup rules" for which one)
2. Create `<Service>.csproj` with `<ProjectReference>`s to whichever `common/DotNetMonoRepoTemplate.*` libraries the service actually needs (see `common-packages.md`'s library table — don't reference every library reflexively)
3. Assign the next available port (check existing: 4000, 4001, 4002, 4003) and set it in `Configuration/<Service>Options.cs`
4. Register the new service in root `docker-compose.yaml` (once its image-build pipeline exists — see `deployment-coolify.md`)
5. Add a YARP route in `api-gateway`'s `Program.cs` if the service should be reachable through the gateway
6. Create a multi-stage `Dockerfile` at `apps/backend/<service>/Dockerfile` (see `rules/docker.md` for the canonical .NET template)
7. Create `.env.example`
8. Add the new project to `DotNetMonoRepoTemplate.sln` (a new GUID via `python3 -c "import uuid; print(str(uuid.uuid4()).upper())"`, both the `Project(...)` entry and its four `ProjectConfigurationPlatforms` lines — see any recent `.sln` diff for the exact shape, and double-check no existing entry was accidentally dropped, a real bookkeeping mistake made and caught once already during this migration)
9. Run `dotnet build DotNetMonoRepoTemplate.sln` to confirm the new project resolves and builds clean

Checklist: `Program.cs`, `<Service>.csproj`, `appsettings.json`, `Configuration/<Service>Options.cs` + `Validator.cs` + `Factory.cs`, `Auth/AuthGuardMiddleware.cs` + `CurrentUser.cs` + `RequirePermissionsAttribute.cs` (if the service needs auth — `schedule-api`'s API-key auth is the exception, not the rule), at least one stub `Dtos/`/`Validators/`/`Services/`/`Endpoints/` file, `Middleware/{SecurityHeaders,RequestLogging,SensitiveDataMasker,ApiVersion,AppExceptionHandler}Middleware.cs` (copy verbatim from the nearest existing service, they're identical across services except namespace), `tests/` folder (empty is fine — no test convention has actually been exercised yet, see `testing.md`), `.env.example`, `Dockerfile`, `README.md`.

## Scaffolding a new React frontend app — unchanged

Vite + React + TypeScript, Tailwind v4 with PostCSS, `src/services/apiClient.ts` (Axios), Vite proxy for `/api`, React Router, React Query provider, admin layout (sidenav + topnav + content) if it's an admin app.

Checklist: Vite config with proxy, `postcss.config.js` with `@tailwindcss/postcss`, `tailwind.config.ts` with CSS variable tokens, `src/index.css` with `:root`/`@theme`/`@layer base`, `src/services/apiClient.ts` with interceptors, React Query provider, React Router, `.env` + `.env.example` with `VITE_<SCOPE>_API_BASE_URL`, access + refresh token held in memory only (the backend returns both in the JSON response body, not a cookie — see `jwt-security.md`), `Dockerfile`, `README.md`.

## Scaffolding a new Next.js app — unchanged

Latest stable Next.js with TypeScript, Tailwind v4 with PostCSS, `app/services/apiClient.ts` (Axios), metadata/SEO on all pages, cookie consent component.

Checklist: `next.config.mjs`, `postcss.config.js`, `tailwind.config.ts`, `app/globals.css`, `app/services/apiClient.ts`, React Query provider in root layout, `app/sitemap.ts` + `app/robots.ts`, metadata on all pages, cookie consent, `.env` + `.env.example` with `NEXT_PUBLIC_<SCOPE>_API_BASE_URL`, `Dockerfile`, `README.md`.

## Scaffolding a new common C# library

```
common/DotNetMonoRepoTemplate.<Name>/
├── DotNetMonoRepoTemplate.<Name>.csproj
├── <PublicType>.cs
└── <Name>ServiceCollectionExtensions.cs   # if the library needs DI registration
```

No `src/index.ts` barrel-export equivalent — the C# namespace itself is the public surface. Add every new `PackageReference` version to root `Directory.Packages.props` (confirm the version live against `api.nuget.org`'s flatcontainer API, never guessed), add the project to `DotNetMonoRepoTemplate.sln`, and reference it from consuming services via `<ProjectReference>`.

## docker-compose.yaml entry for a new backend service

```yaml
[service-name]:
  image: ghcr.io/node-mono-repo-template/[service-name]:main
  pull_policy: always
  environment:
    - DATABASE_URL=${DATABASE_URL}
  depends_on:
    api-gateway:
      condition: service_started
```

No `build:` block — see `rules/docker.md`'s "Root docker-compose.yaml" section: images are built by GitHub Actions and pushed to GHCR, Coolify only pulls.

## Environment variable template (backend)

```env
NODE_ENV=development
DOTNET_ENVIRONMENT=Development
PORT=4004
DATABASE_URL=postgresql://user:password@localhost:5432/dbname
JWT_SECRET=REPLACE_WITH_64_BYTE_HEX_SECRET
JWT_REFRESH_SECRET=REPLACE_WITH_64_BYTE_HEX_SECRET
REDIS_URL=redis://localhost:6379
LOG_LEVEL=info
```

Only include `JWT_*` if the service actually issues/validates its own tokens (`schedule-api` doesn't — it uses API-key auth instead, see its `Auth/ApiKeyMiddleware.cs`). Always add real values to `.env` (gitignored, developer-created) and placeholders to `.env.example` (committed).

## After scaffolding

```bash
dotnet build DotNetMonoRepoTemplate.sln
dotnet run --project apps/backend/<service-name>/src/<Service>.csproj
pnpm install    # only if a frontend/mobile app was also scaffolded
```

Zero build errors and zero warnings before marking the scaffold complete (nullable warnings are errors — see `csharp-standards.md`).
