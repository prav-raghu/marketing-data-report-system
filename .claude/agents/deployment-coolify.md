---
name: deployment-coolify
description: Use for deploying this project to a self-hosted VPS via Coolify — the canonical deployment path for this template. Covers Coolify applications, multi-stage .NET Dockerfile builds (backend) and pnpm builds (frontend), environment variables, managed Postgres/Redis resources, the post-deploy migration command, GitHub-push auto-deploy, and DNS/Cloudflare setup. Requires the VPS to already be bootstrapped and Coolify installed — see vps-bootstrap for that prerequisite. Trigger on "deploy with Coolify", "Coolify application", "deploy config", or "set up CI/CD for the VPS".
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

This is the canonical, only sanctioned deployment path for projects derived from this template. The VPS must already be bootstrapped and have Coolify installed (see `vps-bootstrap`) before any of this applies. Backend services are ASP.NET Core (.NET 10) now, built with multi-stage `dotnet` Dockerfiles; frontend apps are unchanged (pnpm/Vite/Next.js).

**As of the .NET migration, several pieces this doc describes have not actually been updated yet** — flagged inline below rather than silently presented as done. Treat those as Phase 6 follow-up work when you encounter them, not as already-wired.

## Non-negotiable rules

Every project deploys to a dedicated VPS — no shared hosting across clients. Coolify is the only deployment tool — no Kamal, no Dokploy, no manual Docker commands, no raw SSH scripts. All secrets live in Coolify's per-application Environment Variables (marked as secret/build-time as appropriate) — never in committed `.env` files, never hardcoded. Backend Dockerfiles use NuGet Central Package Management (`Directory.Packages.props`) — no per-`.csproj` package versions. Frontend Dockerfiles still need `pnpm-workspace.yaml` copied before `pnpm install` (pnpm v11 requirement — see "pnpm v11 requirements (frontend only)" below). Database and Redis run as Coolify-managed resources on the same project, never external managed services unless explicitly specified. **Images are built by GitHub Actions and pushed to GHCR — Coolify only pulls prebuilt images, it never runs `docker build` on the VPS** (see "Image build strategy" below). The API deploys before any frontend.

## Image build strategy — GitHub Actions builds, Coolify only pulls

**This project does not build Dockerfiles on the VPS.** Building 6 images concurrently (4 .NET multi-stage, 2 pnpm) on a CX22/CX32 VPS competes with the very containers it's trying to serve, so the build step is offloaded to GitHub Actions. Coolify's Build Pack stays **Docker Compose** — the compose file's per-service `build:` blocks are replaced with `image: ghcr.io/node-mono-repo-template/<service>:main`, so Coolify's compose step pulls each image from GHCR instead of building it.

```
git push main
  → .github/workflows/continuous-integration.yml (GitHub-hosted runner)
      → path-filtered per service (dorny/paths-filter) — only rebuilds services whose files changed
      → docker/build-push-action builds each Dockerfile (context = repo root, same Dockerfiles as before)
      → pushes ghcr.io/node-mono-repo-template/<service>:main and :sha-<short-sha>
  → Coolify (webhook or polling) sees the new :main image digest → pulls it → zero-downtime swap
```

**This workflow file is currently fully commented out** (`.github/workflows/continuous-integration.yml`) and still describes the pnpm/Prisma-era pipeline where it isn't — it needs a real rewrite for the four .NET services (setup: no Node/pnpm steps needed for them, just `docker/build-push-action` against the new Dockerfiles) before this build strategy is actually live. Don't assume it's running.

**Registry**: GHCR (`ghcr.io/node-mono-repo-template/<service>`), auth via the workflow's built-in `GITHUB_TOKEN` (`packages: write` permission) — no separate registry account needed for push.

**Coolify side (one-time setup on the Docker Compose resource)**: unchanged from the Node era — see the original setup steps in "First deploy checklist" below. GHCR pull credentials and `pull_policy: always` behavior don't care what language built the image.

**Rollback**: every build also pushes a `:sha-<short-sha>` tag. To roll back, change the Coolify application's image tag from `:main` to the last-known-good `:sha-...` and redeploy — no rebuild needed.

## VPS specification

| Resource | Minimum | Recommended |
|---|---|---|
| Provider | Hetzner Cloud | Hetzner Cloud |
| Instance | CX22 (2 vCPU, 4 GB) | CX32 (4 vCPU, 8 GB) |
| OS | Ubuntu 24.04 LTS | Ubuntu 24.04 LTS |
| Storage | 40 GB SSD | 80 GB SSD |
| Region | Closest to client | EU (Falkenstein) default |

Coolify installs and manages its own Traefik proxy, SSL (Let's Encrypt), and container orchestration on top of the bootstrapped server — nothing further is installed bare-metal.

## Deployment architecture — single Docker Compose stack

Coolify deploys this repo as **one Docker Compose resource** (Build Pack = Docker Compose). The root `docker-compose.yaml` declares every service as `image: ghcr.io/node-mono-repo-template/<service>:main` — Coolify **pulls** each from GHCR, it does not build from the Dockerfiles. Postgres and Redis are separate Coolify-managed resources.

```
GitHub Repo
└── .github/workflows/continuous-integration.yml builds & pushes to GHCR (currently commented out — needs a .NET-era rewrite)
└── Coolify Resource: Docker Compose  →  docker-compose.yaml (image: only, no build:)
        ├── migrate        ghcr.io/node-mono-repo-template/admin-api:main       (runs the migration command, exits — see "Post-deploy migration command")
        ├── api-gateway     ghcr.io/node-mono-repo-template/api-gateway:main      EXPOSE 4000  (.NET)
        ├── admin-api       ghcr.io/node-mono-repo-template/admin-api:main        EXPOSE 4001  (.NET)
        ├── customer-api    ghcr.io/node-mono-repo-template/customer-api:main     EXPOSE 4002  (.NET)
        ├── schedule-api    ghcr.io/node-mono-repo-template/schedule-api:main     EXPOSE 4003  (.NET)
        ├── customer-web    ghcr.io/node-mono-repo-template/customer-web:main     EXPOSE 3000  (Next.js, unchanged)
        └── admin-web       ghcr.io/node-mono-repo-template/admin-web:main        EXPOSE 80    (Vite/nginx, unchanged)

Managed separately in Coolify:
        PostgreSQL resource  →  DATABASE_URL
        Redis resource       →  REDIS_URL
```

**The root `docker-compose.yaml`'s backend image references and the `migrate` service definition have not been updated for .NET yet** — as of this writing they still describe the old `prisma migrate deploy`-based flow. See `documentation/dotnet-migration-plan.md`'s progress log for the current state before assuming this file matches what's below.

Dockerfiles live **at each app root**, not in `devops/`. Build context is always the **monorepo root**.

## Backend Dockerfiles — multi-stage .NET (canonical pattern, shown for `customer-api`)

See `rules/docker.md` for the full canonical version and the EXPOSE/healthcheck table. Summary:

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
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4002
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4002/api/v1/ping || exit 1
ENTRYPOINT ["dotnet", "CustomerApi.dll"]
```

Two stages (`build`/`runtime`), not three — `dotnet publish` already produces a lean output, no separate prune step the way pnpm needed. All four backend Dockerfiles (`api-gateway`, `admin-api`, `customer-api`, `schedule-api`) already exist and follow this exact pattern — check the real files rather than re-deriving from scratch.

## Frontend Dockerfiles — pnpm, unchanged

`admin-web` and `customer-web` Dockerfiles live at `apps/frontend/admin-web/Dockerfile` and `apps/frontend/customer-web/Dockerfile`, unaffected by the backend migration — still Node/pnpm multi-stage builds. See `rules/docker.md` for the full canonical versions.

- `admin-web` — Vite SPA built to `dist/`, served by `nginx:alpine` on port `80`. Build args: `VITE_ADMIN_API_BASE_URL`, `VITE_ADMIN_APP_NAME`.
- `customer-web` — Next.js with `output: "standalone"`; the runner copies `.next/standalone`, `.next/static`, and `public`, runs `node apps/frontend/customer-web/server.js` on `:3000` (`PORT`/`HOSTNAME` env). Build args: `NEXT_PUBLIC_CUSTOMER_API_BASE_URL`, `NEXT_PUBLIC_CUSTOMER_APP_NAME`.
- The `NEXT_PUBLIC_*` / `VITE_*` args are declared before the build step and must be set as **Build Variables** in GitHub Actions (the workflow that builds these images), not in Coolify.

## pnpm v11 requirements (frontend Dockerfiles only)

`pnpm-workspace.yaml` must include:

```yaml
packages:
    - "apps/frontend/*"
    - "apps/mobile/*"
    # apps/backend is ASP.NET Core (.NET 10) now — dotnet CLI + NuGet CPM, not a pnpm workspace member
    # apps/cms is excluded — Piranha CMS (.NET 10) now, not npm-managed at all
    # common/* is C# class libraries (DotNetMonoRepoTemplate.*) now — not a pnpm workspace member

verifyDepsBeforeRun: false

allowBuilds:
    "@sentry/cli": true
    core-js: true
    cypress: true
    esbuild: true
    sharp: true
    unrs-resolver: true
```

`apps/backend/*`, `apps/cms`, and `common/*` are not pnpm workspace members at all anymore (removed from `packages:` entirely, not left as harmless empty globs — all Node source under those paths was deleted in the migration) — don't add them back assuming a backend, CMS, or `common/*` package still exists in this workspace. `allowBuilds` no longer needs `@prisma/engines`/`bcrypt`/`prisma`/`@apollo/protobufjs`/`protobufjs`/`msgpackr-extract` — those were backend-only native deps, gone with the Node backend.

## Coolify applications

| Service | GHCR image | Port | Domain (FQDN) | Health check |
|---|---|---|---|---|
| api-gateway | `ghcr.io/node-mono-repo-template/api-gateway` | `4000` | `https://api.<domain>` | `/health` (`4000`) |
| customer-api | `ghcr.io/node-mono-repo-template/customer-api` | `4002` | internal | `/api/v1/ping` (`4002`) |
| admin-api | `ghcr.io/node-mono-repo-template/admin-api` | `4001` | internal | `/api/v1/ping` (`4001`) |
| schedule-api | `ghcr.io/node-mono-repo-template/schedule-api` | `4003` | internal | `/api/v1/ping` (`4003`) |
| admin-web | `ghcr.io/node-mono-repo-template/admin-web` | `80` | `https://admin.<domain>` | `/` (`80`) |
| customer-web | `ghcr.io/node-mono-repo-template/customer-web` | `3000` | `https://<domain>` | `/` (`3000`) |

Coolify still reads the exposed port from each Dockerfile's `EXPOSE` (baked into the image at GitHub Actions build time) and routes the configured domain to it through its managed Traefik proxy — SSL is provisioned automatically via Let's Encrypt once DNS resolves to the VPS.

## Managed resources (Postgres + Redis)

Unchanged from the Node era — create as Coolify **Resources** in the same project:

- **PostgreSQL** — Coolify's PostgreSQL resource. EF Core (Npgsql) connects the same way Prisma did — same connection-string format, same internal networking.
- **Redis** — Coolify's Redis resource with a password set.

Reference these from each API application by their **internal connection URL**. Set `DATABASE_URL` and `REDIS_URL` on every backend application to those internal URLs.

## Environment variables

Set these on each application under **Environment Variables** in Coolify. Mark secrets as secret; values consumed only during `docker build` (e.g. `NEXT_PUBLIC_*`) must be flagged **Build Variable** so they are baked into the image.

### Backend applications (all four)

| Variable | Notes |
|---|---|
| `NODE_ENV` | `production` (still read for app-level environment semantics — see `env-config.instructions.md`) |
| `DOTNET_ENVIRONMENT` | `Production` |
| `PORT` | per-service, see the port table above |
| `DATABASE_URL` | internal Postgres URL from the Coolify resource |
| `REDIS_URL` | internal Redis URL from the Coolify resource |
| `JWT_SECRET`, `JWT_REFRESH_SECRET` | secret — admin-api, customer-api |
| `TWO_FACTOR_ENCRYPTION_KEY` | secret, exactly 64 hex chars — admin-api |
| `CORS_ORIGIN` | the customer-web/admin origins |
| `MAILTRAP_API_KEY`, `MAILTRAP_FROM`, `MAILTRAP_FROM_NAME` | secret — admin-api, customer-api |
| `SMSPORTAL_CLIENT_ID`, `SMSPORTAL_API_SECRET` | secret — wherever `DotNetMonoRepoTemplate.Sms` is used |
| `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_STORAGE_CONTAINER` | secret (if using Azure storage via `DotNetMonoRepoTemplate.Storage`) |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `AWS_S3_BUCKET` | secret (if using S3 via `DotNetMonoRepoTemplate.Storage`) |

### Frontend applications — unchanged

| Variable | Notes |
|---|---|
| `NODE_ENV` | `production` |
| `NEXT_PUBLIC_CUSTOMER_API_BASE_URL` | customer-web — **Build Variable**, baked at build time |
| `VITE_ADMIN_API_BASE_URL` | admin-web — **Build Variable**, baked at build time |

Never commit these values anywhere — Coolify stores and injects them at deploy time (runtime vars) or GitHub Actions injects them as build args (`NEXT_PUBLIC_*`/`VITE_*`).

## Common library env vars (who reads what)

| Library | Environment Variables |
|---|---|
| `DotNetMonoRepoTemplate.Database` | `DATABASE_URL` |
| `DotNetMonoRepoTemplate.Cache` | `REDIS_URL`, `REDIS_TLS_REJECT_UNAUTHORIZED` |
| `DotNetMonoRepoTemplate.Queue` | `REDIS_URL` (Hangfire's Redis storage) |
| `DotNetMonoRepoTemplate.Email` | `MAILTRAP_API_KEY`, `MAILTRAP_FROM`, `MAILTRAP_FROM_NAME` |
| `DotNetMonoRepoTemplate.Sms` | `SMSPORTAL_CLIENT_ID`, `SMSPORTAL_API_SECRET` |
| `DotNetMonoRepoTemplate.Storage` | `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_STORAGE_CONTAINER`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `AWS_S3_BUCKET` |
| `DotNetMonoRepoTemplate.Observability` | `SENTRY_DSN`, `SENTRY_RELEASE`, `SENTRY_TRACES_SAMPLE_RATE`, `OTEL_EXPORTER_OTLP_*` |
| `DotNetMonoRepoTemplate.Metrics` | `PORT` (Prometheus scrape port, via `prometheus-net.AspNetCore`) |

There is no `DotNetMonoRepoTemplate.Configuration` equivalent to the old `common/config` package — env config is per-service now via each service's `<Service>Options`/`OptionsFactory` (see `common-packages.md`).

## CI/CD — GitHub Actions builds, Coolify redeploys on new image

Two separate triggers cooperate, both firing off the same push to `main` — **once `continuous-integration.yml` is rewritten for .NET** (currently fully commented out, still describing the pnpm/Prisma pipeline):

1. **`.github/workflows/continuous-integration.yml`** — path-filtered per service, builds only the images whose files changed, pushes `:main` and `:sha-<short-sha>` to GHCR. For the four backend services this becomes a straightforward `docker/build-push-action` step against each `apps/backend/*/Dockerfile` — no `pnpm install`/`turbo run build` steps needed for those, since the Dockerfile itself does `dotnet restore`/`dotnet publish`.
2. **Coolify** — configured on the single Docker Compose resource with Auto Deploy on, force-pulling on the GHCR webhook or via polling.

## Post-deploy migration command — needs a .NET rewrite

**This is not yet wired up for .NET.** The Node-era mechanism ran `prisma migrate deploy` as the API application's Coolify Post-deployment Command. The .NET equivalent, once EF Core migrations exist (see `ef-core.md`/`database-migrations.md` — no migrations exist yet in this repo), is:

```bash
#!/bin/sh
set -e
echo "Running dotnet ef database update..."
dotnet CustomerApi.dll --migrate   # illustrative — the actual invocation depends on how migration-at-startup vs. a dedicated migration entrypoint gets designed; do not assume this exact flag exists
```

Running EF Core migrations from inside a published, already-`dotnet publish`'d container is a different shape of problem than Node's `prisma migrate deploy` CLI invocation (the `dotnet-ef` tool isn't included in a runtime-only `mcr.microsoft.com/dotnet/aspnet` image by design) — this needs a real design decision (a separate migration-runner image built from the SDK stage, or a startup migration hook with a distributed lock to prevent multi-replica races) before it's implemented, not a copy-paste translation of the Prisma script. Flag this to the developer rather than inventing an untested mechanism.

## First deploy checklist (once per project)

1. VPS bootstrapped and Coolify installed — see `vps-bootstrap`.
2. Ensure `.github/workflows/continuous-integration.yml` is rewritten for .NET and has run at least once on `main` (all 6 images pushed to GHCR) — Coolify's first pull needs the images to already exist.
3. In Coolify: create a **Project**, add a Docker Registry credential for GHCR (username = GitHub username, password = a GitHub PAT with `read:packages`).
4. Create **PostgreSQL** and **Redis** as Coolify-managed resources. Copy their internal connection strings.
5. Create a **Docker Compose** resource: Build Pack = Docker Compose, Base Directory = `/`, Compose file = `docker-compose.yaml` (once its backend `image:`/`migrate` definitions are updated for .NET).
6. In the resource's **Environment Variables** panel, add every var from the tables above with real values.
7. Set domains: `api.<domain>` on api-gateway (port 4000), `admin.<domain>` on admin-web (port 80), `<domain>` on customer-web (port 3000).
8. Point DNS at the VPS, then trigger the first deploy from the Coolify UI.

## Per-project substitutions

| Placeholder | Replace with |
|---|---|
| `<project>` | e.g. `my-project` |
| `<domain>` | e.g. `myproject.co.za` |

## DNS and Cloudflare — unchanged

One VPS IP per project. Coolify's Traefik proxy listens on 80/443 and routes by `Host` header.

```
DNS (Cloudflare)        Traefik (VPS :443)     Container
<domain>            ──► customer-web           :3000 (Next.js)
admin.<domain>      ──► admin-web              :80   (nginx)
api.<domain>        ──► api-gateway            :4000
```

`admin-api`, `customer-api`, and `schedule-api` are internal. Use a wildcard A record:

```
A   <domain>      <VPS_IP>   Proxied
A   *.<domain>    <VPS_IP>   Proxied
```

Cloudflare SSL mode must be Full (strict).

## What Claude Code must not do

Never trigger a Coolify deploy — the developer does this from the Coolify UI or via git push. Never run migrations manually — the (to-be-designed) post-deploy mechanism handles it. Never run `git` commands. Never hardcode secret values. Never modify `pnpm-workspace.yaml` package globs or `common/*` structure to fit deployment needs. Never add a `build:` block back to `docker-compose.yaml` — it must stay `image:`-only. Never invent an untested EF Core migration-execution mechanism and present it as already working — flag the gap to the developer instead (see "Post-deploy migration command" above).
