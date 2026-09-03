---
paths:
  - "**/Dockerfile"
  - "docker-compose*.yaml"
  - "docker-compose*.yml"
---

# Docker Rules

You are working on Docker configuration for this monorepo. Backend services and, since Phase 4 of the Blazor migration, frontend apps too are ASP.NET Core / Blazor (.NET 10) now. `apps/mobile/customer-mobile` is .NET MAUI Blazor Hybrid (C#) and isn't Dockerized (native mobile builds, not a deployable container) — same as before, just a different mobile stack.

## Monorepo structure

```
/ (monorepo root)
├── docker-compose.yaml          ← single Coolify production stack (all deployable services)
├── apps/
│   ├── backend/
│   │   ├── api-gateway/   └── Dockerfile   (.NET, multi-stage sdk→aspnet)
│   │   ├── admin-api/     └── Dockerfile   (.NET, multi-stage sdk→aspnet)
│   │   ├── customer-api/  └── Dockerfile   (.NET, multi-stage sdk→aspnet)
│   │   └── schedule-api/  └── Dockerfile   (.NET, multi-stage sdk→aspnet)
│   └── frontend/
│       ├── admin-web/     └── Dockerfile   (Blazor WASM Standalone → nginx, since Phase 4)
│       └── customer-web/  └── Dockerfile   (Blazor Web App, ASP.NET Core host, since Phase 4)
├── common/                      ← shared libs only, never a Dockerfile (C# class libraries copied in as build context, not separately imaged)
└── devops/                      ← local dev infra + scripts only, never deployed
    └── docker-compose.dev.yml   ← local dev only (Postgres, Redis, Mailhog, Adminer)
```

One `Dockerfile` per deployable app, located at the app root. `devops/` contains local infrastructure and scripts only — never a Dockerfile. `common/*` are shared libraries — never get their own Dockerfile; the backend build stage `COPY`s the whole `common/` directory so `<ProjectReference>`s resolve (Blazor apps under `apps/frontend/*` currently have no `<ProjectReference>` into `common/*`, so their build stages don't need it — see each app's own Dockerfile). Do not create `docker-compose.qa.yml` or `docker-compose.prod.yml` — Coolify environments handle this.

## Build context is always the monorepo root

Every Dockerfile uses the repo root as its build context. Backend Dockerfiles need root `Directory.Build.props`/`Directory.Packages.props`, `common/`, and the target service. `apps/frontend/*` Blazor Dockerfiles need the same root `Directory.Build.props`/`Directory.Packages.props` (NuGet Central Package Management is solution-wide, covers `apps/frontend/*` too since Phase 4) plus a Node stage for the Tailwind CLI build — see the Blazor Dockerfile section below. The Dockerfile location is inside the app, but the context is always `/`.

## Dockerfile location

```
apps/backend/<service>/Dockerfile
apps/frontend/<app>/Dockerfile
```

Never put a Dockerfile at the monorepo root or in `devops/`.

## Multi-stage pattern — backend (.NET, required)

| Stage | Purpose |
|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` — copy `Directory.Build.props`/`Directory.Packages.props`, `common/`, the target service, `dotnet restore`, `dotnet publish -c Release -o /app/publish --no-restore` |
| `runtime` | `mcr.microsoft.com/dotnet/aspnet:10.0` — non-root user, published output only, `ENTRYPOINT ["dotnet", "<Service>.dll"]` |

Two stages, not three — there's no separate "prune"/"deploy" step the way pnpm needed; `dotnet publish` already produces a self-contained runtime folder with only what's needed.

## Canonical backend Dockerfile (shown for `customer-api`, EXPOSE 4002)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY common/ ./common/
COPY apps/backend/customer-api/ ./apps/backend/customer-api/
RUN dotnet restore apps/backend/customer-api/src/CustomerApi.csproj
RUN dotnet publish apps/backend/customer-api/src/CustomerApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4002
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4002/api/v1/ping || exit 1
ENTRYPOINT ["dotnet", "CustomerApi.dll"]
```

Per service, change the `.csproj` path, the `COPY apps/backend/<service>/` path, `EXPOSE`, the healthcheck port/route, and the `ENTRYPOINT` DLL name to match the table below. `curl` is **not** present by default in `mcr.microsoft.com/dotnet/aspnet` (Debian-slim base) — the `apt-get install curl` line above in the `runtime` stage is required, not optional, for the `HEALTHCHECK` directive to execute at all (as opposed to failing the check, it fails to run the command — `curl: not found`). `docker-compose.yaml`'s own healthcheck `test:` blocks (which `docker exec` into the already-running container) use `curl -f` too, for the same reason — keep both in sync if either changes.

## Blazor frontend Dockerfiles — since Phase 4 (replaces the old pnpm/Node pattern)

`admin-web` and `customer-web` build with `dotnet` now, not pnpm — but each keeps a small Tailwind-only `package.json`, so their Dockerfiles still open with a Node stage that runs the Tailwind CLI and hands the built CSS to the `dotnet` build stage. See below for their canonical Dockerfiles.

## Correct EXPOSE values (do not copy from other sources)

| Service | `EXPOSE` | Healthcheck route |
|---|---|---|
| `apps/backend/api-gateway/Dockerfile` | `4000` | `/health` (gateway's own aggregation route, not `/api/v1/ping`) |
| `apps/backend/admin-api/Dockerfile` | `4001` | `/api/v1/ping` |
| `apps/backend/customer-api/Dockerfile` | `4002` | `/api/v1/ping` |
| `apps/backend/schedule-api/Dockerfile` | `4003` | `/api/v1/ping` |
| `apps/frontend/admin-web/Dockerfile` | `80` | n/a (nginx serving the published `wwwroot`) |
| `apps/frontend/customer-web/Dockerfile` | `3000` | n/a (ASP.NET Core host, no health route mapped yet) |

The 3000–3003 range is wrong for backend services and will cause Coolify port-detection to misfire. `EXPOSE` must always match the `PORT` env var the service reads at runtime (via `<Service>Options.Port`, bound in `Program.cs` with `builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}")`) — for `customer-web` specifically, that's `ASPNETCORE_URLS`, not a custom `<Service>Options.Port`, since it's a Blazor Web App using ASP.NET Core's own hosting conventions rather than this repo's backend `Options` pattern.

## Healthcheck must use 127.0.0.1, not localhost

Alpine/slim containers can resolve `localhost` to IPv6 `::1`, and Kestrel binds IPv4 by default in these images. Always:

```dockerfile
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:<PORT>/<health-route> || exit 1
```

## Blazor Web App Dockerfile (`customer-web`, ASP.NET Core host, EXPOSE 3000) — since Phase 4

Two-project app (`src/CustomerWeb.csproj`, the server host, plus `src/Client/CustomerWeb.Client.csproj`, the WASM project for anything needing `@rendermode="InteractiveWebAssembly"` — see `frontend-blazor.md` for why). Publishing the server project transitively builds and bundles the Client project's WASM output — only the server `.csproj` needs `dotnet publish`.

```dockerfile
FROM node:22-alpine AS css-builder
WORKDIR /app
COPY apps/frontend/customer-web/package.json ./
RUN npm install
COPY apps/frontend/customer-web/src/wwwroot/css/input.css ./src/wwwroot/css/input.css
COPY apps/frontend/customer-web/src/Pages ./src/Pages
COPY apps/frontend/customer-web/src/Layout ./src/Layout
COPY apps/frontend/customer-web/src/App.razor ./src/App.razor
COPY apps/frontend/customer-web/src/Routes.razor ./src/Routes.razor
COPY apps/frontend/customer-web/src/Client/Pages ./src/Client/Pages
RUN npx @tailwindcss/cli -i ./src/wwwroot/css/input.css -o ./src/wwwroot/css/app.css --minify

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY apps/frontend/customer-web/ ./apps/frontend/customer-web/
COPY --from=css-builder /app/src/wwwroot/css/app.css ./apps/frontend/customer-web/src/wwwroot/css/app.css
RUN dotnet restore apps/frontend/customer-web/src/CustomerWeb.csproj
RUN dotnet publish apps/frontend/customer-web/src/CustomerWeb.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_URLS=http://0.0.0.0:3000
EXPOSE 3000
ENTRYPOINT ["dotnet", "CustomerWeb.dll"]
```

The Node `css-builder` stage only `COPY`s the specific folders Tailwind's `@source` directive needs to scan (`.razor` files, both server and Client projects) — never `COPY apps/frontend/customer-web/ .` there, that stage has no use for the `.csproj`/`Program.cs`/C# source. `dotnet restore`/`dotnet publish` target only the server `.csproj` — the `<ProjectReference>` to `Client/CustomerWeb.Client.csproj` pulls the WASM build in automatically as part of that publish. No build-args needed — this app reads config from `appsettings.json`, not baked-in `NEXT_PUBLIC_*`-style vars.

## Blazor WebAssembly Standalone Dockerfile (`admin-web`, EXPOSE 80) — since Phase 4

```dockerfile
FROM node:22-alpine AS css-builder
WORKDIR /app
COPY apps/frontend/admin-web/package.json ./
RUN npm install
COPY apps/frontend/admin-web/src/wwwroot/css/input.css ./src/wwwroot/css/input.css
COPY apps/frontend/admin-web/src/Pages ./src/Pages
COPY apps/frontend/admin-web/src/Layout ./src/Layout
COPY apps/frontend/admin-web/src/Components ./src/Components
COPY apps/frontend/admin-web/src/App.razor ./src/App.razor
COPY apps/frontend/admin-web/src/RedirectToLogin.razor ./src/RedirectToLogin.razor
RUN npx @tailwindcss/cli -i ./src/wwwroot/css/input.css -o ./src/wwwroot/css/app.css --minify

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY apps/frontend/admin-web/ ./apps/frontend/admin-web/
COPY --from=css-builder /app/src/wwwroot/css/app.css ./apps/frontend/admin-web/src/wwwroot/css/app.css
RUN dotnet restore apps/frontend/admin-web/src/AdminWeb.csproj
RUN dotnet publish apps/frontend/admin-web/src/AdminWeb.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS runner
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY infrastructure/nginx/admin-web.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

A Blazor WASM Standalone `dotnet publish` produces a static `wwwroot/` — same shape as a Vite `dist/`, so the retired React app's nginx config (`infrastructure/nginx/admin-web.conf`) is reused unchanged, not rewritten. No build-args needed here either — config goes through `wwwroot/appsettings.json`/`appsettings.{Environment}.json`, not baked-in `VITE_*` vars. Frontend GHCR image names are **unscoped** (`admin-web`, `customer-web`).

## `.dockerignore`

Place at the monorepo root:

```
node_modules
.next
dist
bin
obj
.turbo
.git
*.log
.env
coverage
```

`bin`/`obj` are the .NET build-output directories — exclude them the same way `dist`/`.next` are excluded for the frontend, so a stale local build never gets copied into a Docker build context.

## Root docker-compose.yaml

This is the single Coolify deployment stack. Every deployable service is declared here. Do not create `docker-compose.prod.yml` or `docker-compose.qa.yml` — Coolify manages environments.

`devops/docker-compose.dev.yml` is for local development only, never deployed.

**Images are built by GitHub Actions and pushed to GHCR — Coolify never builds.** The committed root `docker-compose.yaml` declares every service as `image: ghcr.io/node-mono-repo-template/<service>:main` with `pull_policy: always`; there is no `build:` block anywhere in that file. `.github/workflows/continuous-integration.yml`'s `docker-build` job builds each `apps/*/Dockerfile` (the same Dockerfiles documented above, including the two Blazor frontend ones added in Phase 4) on a path-filtered matrix, pushes `:main` and `:sha-<short-sha>` tags to GHCR, and Coolify's Docker Compose resource only pulls and swaps containers. See the `deployment-coolify` agent's "Image build strategy" section for the full flow. **The `migrate` one-shot service is still a documented no-op (`echo ... && exit 0`), not a real `dotnet ef database update` step — no EF Core migrations exist in this repo yet, see `ef-core.md`. That's a real, still-open gap; it is not related to the frontend Blazor cutover.**

## Container startup ordering — depends_on

Since Coolify only pulls prebuilt images, `depends_on` in `docker-compose.yaml` controls **startup order**, not build order. The `migrate` one-shot service currently runs `prisma migrate deploy` and exits — once EF Core migrations exist (see `ef-core.md`), this becomes `dotnet ef database update` run from a throwaway container built off the same image, or a startup-time migration step; `admin-api`/`customer-api`/`schedule-api` gate on it with `condition: service_completed_successfully`, and `api-gateway` gates on the APIs with `condition: service_healthy` so it never proxies to a not-yet-ready backend. Do not reference services outside this compose file (e.g. Coolify-managed Postgres/Redis) in `depends_on` — reach those only via `DATABASE_URL`/`REDIS_URL` env vars.

## What not to do

- Do not use `COPY . .` in any Dockerfile — copy only `Directory.Build.props`/`Directory.Packages.props` + `common/` + the target service (backend), or `Directory.Build.props`/`Directory.Packages.props` + the target app (Blazor frontend), or the specific folders Tailwind needs to scan (the Node `css-builder` stage)
- Do not create environment-specific compose files (`docker-compose.qa.yml`, etc.) — Coolify environments handle this
- Do not use `latest` image tags in production — use the `:main` / `:sha-<short-sha>` tags GitHub Actions pushes to GHCR
- Do not copy `.env` files into images — inject via Coolify Environment Variables (runtime); Blazor frontend apps have no build-time env var equivalent to `NEXT_PUBLIC_*`/`VITE_*` anymore, config goes through `wwwroot/appsettings.json` instead
- Do not run apps as root in production — add a non-root user everywhere: backend and `customer-web` (`adduser --disabled-password --gecos "" appuser`), `admin-web` (nginx's own default is fine, same as before)
- Do not copy `EXPOSE` port values from other sources — use the table above
- Do not add a `build:` block back to the root `docker-compose.yaml` — it must stay `image:`-only so Coolify never builds on the VPS
- Do not use `dotnet run` in the runtime stage — always `dotnet publish` in the build stage and run the published DLL directly
- Do not skip `dotnet restore` as an explicit separate `RUN` before `dotnet publish --no-restore` — splitting them lets Docker layer-cache the restore step across rebuilds when only source (not `.csproj`/`Directory.Packages.props`) changed
