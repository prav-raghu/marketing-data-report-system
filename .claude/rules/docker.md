---
paths:
  - "**/Dockerfile"
  - "docker-compose*.yaml"
  - "docker-compose*.yml"
---

# Docker Rules

You are working on Docker configuration for this monorepo. Backend services are ASP.NET Core (.NET 10) now — frontend apps are unchanged (still Node/pnpm).

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
│       ├── admin-web/     └── Dockerfile   (Vite SPA → nginx, unchanged)
│       └── customer-web/  └── Dockerfile   (Next.js standalone, unchanged)
├── common/                      ← shared libs only, never a Dockerfile (C# class libraries copied in as build context, not separately imaged)
└── devops/                      ← local dev infra + scripts only, never deployed
    └── docker-compose.dev.yml   ← local dev only (Postgres, Redis, Mailhog, Adminer)
```

One `Dockerfile` per deployable app, located at the app root. `devops/` contains local infrastructure and scripts only — never a Dockerfile. `common/*` are shared libraries — never get their own Dockerfile; the backend build stage `COPY`s the whole `common/` directory so `<ProjectReference>`s resolve. Do not create `docker-compose.qa.yml` or `docker-compose.prod.yml` — Coolify environments handle this.

## Build context is always the monorepo root

Every Dockerfile uses the repo root as its build context. Backend Dockerfiles need root `Directory.Build.props`/`Directory.Packages.props`, `common/`, and the target service; frontend Dockerfiles need `pnpm-workspace.yaml`/`turbo.json`/`common/` (frontend apps have no build-time dependency on the C# `common/*` libraries, but the build context stays root-relative for consistency). The Dockerfile location is inside the app, but the context is always `/`.

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

## pnpm/frontend Dockerfiles — unchanged from the Node era

`admin-web` and `customer-web` still build with pnpm; nothing here changed for them. See below for their canonical Dockerfiles — the guidance is unchanged, just re-stated for completeness since this rule file now covers both stacks.

## Correct EXPOSE values (do not copy from other sources)

| Service | `EXPOSE` | Healthcheck route |
|---|---|---|
| `apps/backend/api-gateway/Dockerfile` | `4000` | `/health` (gateway's own aggregation route, not `/api/v1/ping`) |
| `apps/backend/admin-api/Dockerfile` | `4001` | `/api/v1/ping` |
| `apps/backend/customer-api/Dockerfile` | `4002` | `/api/v1/ping` |
| `apps/backend/schedule-api/Dockerfile` | `4003` | `/api/v1/ping` |
| `apps/frontend/admin-web/Dockerfile` | `80` | n/a (nginx) |
| `apps/frontend/customer-web/Dockerfile` | `3000` | n/a (Next.js standalone) |

The 3000–3003 range is wrong for backend services and will cause Coolify port-detection to misfire. `EXPOSE` must always match the `PORT` env var the service reads at runtime (via `<Service>Options.Port`, bound in `Program.cs` with `builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}")`).

## Healthcheck must use 127.0.0.1, not localhost

Alpine/slim containers can resolve `localhost` to IPv6 `::1`, and Kestrel binds IPv4 by default in these images. Always:

```dockerfile
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:<PORT>/<health-route> || exit 1
```

## Next.js App Dockerfile (`customer-web`, standalone, EXPOSE 3000) — unchanged

Next.js requires `outputFileTracingRoot` in `next.config.mjs` pointing at the monorepo root so `.next/standalone` preserves the correct nested paths.

```javascript
// apps/frontend/customer-web/next.config.mjs
import path from "path";

const nextConfig = {
  output: "standalone",
  outputFileTracingRoot: path.join(import.meta.dirname, "../../"),
};

export default nextConfig;
```

```dockerfile
FROM node:22-alpine AS base
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
RUN corepack enable

FROM base AS builder
WORKDIR /app
ENV CI=true
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json turbo.json ./
COPY common/ ./common/
COPY apps/frontend/customer-web/ ./apps/frontend/customer-web/
RUN pnpm install --frozen-lockfile
ARG NEXT_PUBLIC_CUSTOMER_API_BASE_URL
ARG NEXT_PUBLIC_CUSTOMER_APP_NAME
ENV NEXT_PUBLIC_CUSTOMER_API_BASE_URL=$NEXT_PUBLIC_CUSTOMER_API_BASE_URL
ENV NEXT_PUBLIC_CUSTOMER_APP_NAME=$NEXT_PUBLIC_CUSTOMER_APP_NAME
RUN pnpm --filter customer-web build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=builder /app/apps/frontend/customer-web/.next/standalone ./
COPY --from=builder /app/apps/frontend/customer-web/.next/static ./apps/frontend/customer-web/.next/static
COPY --from=builder /app/apps/frontend/customer-web/public ./apps/frontend/customer-web/public
EXPOSE 3000
CMD ["node", "apps/frontend/customer-web/server.js"]
```

The `COPY` paths for static files must reflect the nested monorepo path inside `.next/standalone`, not a flat structure. `NEXT_PUBLIC_*` args are declared before the build and must be set as **build args** in `.github/workflows/continuous-integration.yml` (matrix `build-args`) so they bake into the bundle — GitHub Actions builds this image, not Coolify (see below). Note: this CI workflow is currently fully commented out — see `deployment-coolify.md` for the .NET-era rewrite this needs.

## React + Vite App Dockerfile (`admin-web`, EXPOSE 80) — unchanged

```dockerfile
FROM node:22-alpine AS base
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
RUN corepack enable

FROM base AS builder
WORKDIR /app
ENV CI=true
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json turbo.json ./
COPY common/ ./common/
COPY apps/frontend/admin-web/ ./apps/frontend/admin-web/
RUN pnpm install --frozen-lockfile
ARG VITE_ADMIN_API_BASE_URL
ARG VITE_ADMIN_APP_NAME
ENV VITE_ADMIN_API_BASE_URL=$VITE_ADMIN_API_BASE_URL
ENV VITE_ADMIN_APP_NAME=$VITE_ADMIN_APP_NAME
RUN pnpm --filter admin-web build

FROM nginx:alpine AS runner
COPY --from=builder /app/apps/frontend/admin-web/dist /usr/share/nginx/html
COPY infrastructure/nginx/admin-web.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

`VITE_*` args are declared before the build and must be set as **build args** in GitHub Actions so they bake into the bundle. Frontend package names are **unscoped** (`admin-web`, `customer-web`).

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

**Images are built by GitHub Actions and pushed to GHCR — Coolify never builds.** The committed root `docker-compose.yaml` declares every service as `image: ghcr.io/node-mono-repo-template/<service>:main` with `pull_policy: always`; there is no `build:` block anywhere in that file. GitHub Actions builds each `apps/*/Dockerfile` (the same Dockerfiles documented above — backend ones are now multi-stage .NET builds), pushes `:main` and `:sha-<short-sha>` tags to GHCR, and Coolify's Docker Compose resource only pulls and swaps containers. See the `deployment-coolify` agent's "Image build strategy" section for the full flow. **As of the .NET migration, this compose file's backend image references, the `migrate` one-shot service (`prisma migrate deploy`), and `.github/workflows/continuous-integration.yml` (fully commented out, still describes the pnpm/Prisma pipeline) have not yet been updated for the new stack — that's tracked as Phase 6 follow-up work, not something already done.**

## Container startup ordering — depends_on

Since Coolify only pulls prebuilt images, `depends_on` in `docker-compose.yaml` controls **startup order**, not build order. The `migrate` one-shot service currently runs `prisma migrate deploy` and exits — once EF Core migrations exist (see `ef-core.md`), this becomes `dotnet ef database update` run from a throwaway container built off the same image, or a startup-time migration step; `admin-api`/`customer-api`/`schedule-api` gate on it with `condition: service_completed_successfully`, and `api-gateway` gates on the APIs with `condition: service_healthy` so it never proxies to a not-yet-ready backend. Do not reference services outside this compose file (e.g. Coolify-managed Postgres/Redis) in `depends_on` — reach those only via `DATABASE_URL`/`REDIS_URL` env vars.

## What not to do

- Do not use `COPY . .` in any Dockerfile — copy only `Directory.Build.props`/`Directory.Packages.props` + `common/` + the target service (backend), or `pnpm-workspace.yaml` + `common/` + the target app (frontend)
- Do not create environment-specific compose files (`docker-compose.qa.yml`, etc.) — Coolify environments handle this
- Do not use `latest` image tags in production — use the `:main` / `:sha-<short-sha>` tags GitHub Actions pushes to GHCR
- Do not copy `.env` files into images — inject via Coolify Environment Variables (runtime) or GitHub Actions build args (`NEXT_PUBLIC_*`/`VITE_*`)
- Do not run apps as root in production — add a non-root user (backend: `adduser --disabled-password --gecos "" appuser`; frontend: the existing `nodeapp`/nginx defaults)
- Do not copy `EXPOSE` port values from other sources — use the table above
- Do not add a `build:` block back to the root `docker-compose.yaml` — it must stay `image:`-only so Coolify never builds on the VPS
- Do not use `dotnet run` in the runtime stage — always `dotnet publish` in the build stage and run the published DLL directly
- Do not skip `dotnet restore` as an explicit separate `RUN` before `dotnet publish --no-restore` — splitting them lets Docker layer-cache the restore step across rebuilds when only source (not `.csproj`/`Directory.Packages.props`) changed
