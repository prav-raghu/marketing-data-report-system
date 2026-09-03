# CI/CD Pipeline

This document covers the GitHub Actions workflows in `.github/workflows/`, how they fit together, and known issues that were diagnosed and fixed. The pipeline was originally built for the Node/Fastify/Prisma backend (ported from the sibling `zynkosi-tech` project); the "Fixes applied" section below is that history, kept verbatim. Everything above it describes the current, .NET-era pipeline (Phase 6 of `documentation/dotnet-migration-plan.md`).

## Stack split reflected in every workflow

`apps/backend/*` and `common/*` are ASP.NET Core / C# (.NET 10) — `dotnet restore`/`build`/`test` against `DotNetMonoRepoTemplate.sln`, no pnpm involvement. This document predates two later migrations that moved `apps/frontend/*` (admin-web, customer-web) to Blazor in Phase 4 and `apps/mobile/customer-mobile` to .NET MAUI Blazor Hybrid in Phase 5 — both are C#/`dotnet` now too, see `CLAUDE.md`'s "Stack split" section for the current record. pnpm's only remaining role for either is the Tailwind CLI CSS build (`pnpm --filter <app> build:css`) each app's minimal `package.json` exists for. Every workflow that touches both stacks (`continuous-integration.yml`, `security-scan.yml`) runs them as separate parallel jobs rather than one combined pipeline, since there's no shared install step anymore — `common/types` (the old shared TypeScript package) no longer exists.

## Workflow overview

| Workflow | Trigger | Purpose |
|---|---|---|
| `continuous-integration.yml` (display name "Continuous Integration & Deploy") | push/PR to `main`, `develop`; tag `v*.*.*`; manual dispatch | `dotnet build`/`test` for the backend, `pnpm lint`/`typecheck`/`build`/`test` for the frontend, quality-gate, SonarCloud analysis, then — gated behind `quality-gate` passing, and only for pushes to `main`/tags/dispatch — Docker build & push per changed service to GHCR and a Coolify redeploy trigger |
| `pull-request-checks.yml` | PR opened/updated | PR title convention, frontend bundle size comparison |
| `security-scan.yml` | push/PR to `main`, weekly cron | `pnpm audit` (frontend) + `dotnet list package --vulnerable` (backend), CodeQL (`javascript-typescript` and `csharp` in a matrix), secrets scan (TruffleHog) |
| `version-control.yml` | push to `main` | Automated semver + changelog via conventional commits (release-please) — stack-agnostic, untouched by the migration |

`sonarcloud.yml` and `production-build-and-push.yml` no longer exist as separate workflows — both were folded into `continuous-integration.yml`, since they were each re-running a full install+build+test cycle the CI workflow had already just done.

## CI gates the deploy

`docker-build` (and therefore `deploy`) has `needs: [quality-gate, changes]` and an explicit `if: needs.quality-gate.result == 'success' && ...`, so a red `main` can never reach production. `quality-gate` requires the backend build/test job and all four frontend jobs (lint, typecheck, build, test) to succeed first.

## Backend test coverage — a known gap

No `apps/backend/*/tests` project exists anywhere in this repo yet, despite `CLAUDE.md` requiring one per service. `backend-build`'s `dotnet test DotNetMonoRepoTemplate.sln` step is real infrastructure — it just has nothing to run against today, and `dotnet test` against a solution with zero test projects succeeds trivially rather than failing. The moment the first xUnit project lands (see the `testing` subagent), this becomes a real gate with no further workflow changes needed. SonarCloud's backend coverage input has the same gap — see the comment in `continuous-integration.yml`'s `sonarcloud` job and in `sonar-project.properties`.

## Release flow

1. PRs merge to `main` behind `continuous-integration.yml`'s `quality-gate` job (backend build/test + frontend lint/typecheck/build/test must all pass).
2. On every push to `main`, `version-control.yml` (using `googleapis/release-please-action`) scans conventional commits since the last release and maintains a standing `chore(main): release X.Y.Z` PR with a version bump + changelog. This requires `release-please-config.json` and `.release-please-manifest.json` at the repo root — both stack-agnostic (`release-type: simple`, not tied to `package.json` or a `.csproj` version).
3. Merging that PR tags the commit `vX.Y.Z` and cuts a GitHub Release.
4. That tag triggers `continuous-integration.yml`'s `docker-build` job to build all six services (four .NET backend, two frontend) and tag their images with the release version, in addition to the existing `:main` and `:sha-<short>` tags — still gated behind `quality-gate`.
5. The workflow's `deploy` job calls the Coolify deploy webhook, which pulls the new `:main`-tagged images (`pull_policy: always` in `docker-compose.yaml`) and redeploys the stack.

## Coolify deployment

Coolify does not poll GHCR for new image tags — it only redeploys on a git push to the connected repo, or when its deploy webhook is called. `continuous-integration.yml` calls that webhook directly as its final job (`deploy`) once `docker-build` succeeds for all matrix services.

### One-time setup

1. **GHCR registry credential** — In Coolify, go to **Team/Server → Sources → Docker Registries** and add a registry: URL `ghcr.io`, username = a GitHub username, password = a GitHub PAT (classic) with `read:packages` scope. Attach this credential to the Compose resource.
2. **Compose resource** — Create a **Docker Compose** resource in Coolify pointing at this repo, Base Directory `/`, file `docker-compose.yaml`. Every service in that file uses `image:` (no `build:`), so Coolify only parses and pulls — it never builds from source.
3. **Webhook + API token** — Copy the resource's webhook URL from its **Webhooks** tab, and generate an API token under Coolify's **Keys & Tokens** settings. Add both as GitHub Actions repository secrets: `COOLIFY_WEBHOOK_URL` and `COOLIFY_API_TOKEN`.
4. **Runtime environment variables** — Populate `DATABASE_URL`, `REDIS_URL`, `JWT_SECRET`, and the other variables referenced in `docker-compose.yaml` in the Coolify resource's environment settings. These are never baked into images.

### Gotchas

- The registry namespace in `ghcr.io/node-mono-repo-template/<service>` must exactly match the GitHub org/user that owns the packages (case-sensitive) — update it when forking this template.
- If the GitHub org enforces SSO, the PAT used for the registry credential must be authorized for that SSO org.
- `pull_policy: always` in `docker-compose.yaml` is what makes a redeploy fetch the new image digest under the same `:main` tag.
- The `migrate` service in `docker-compose.yaml` is currently a documented no-op — see the comment there and `ef-core.md`. No EF Core migrations exist in this repo yet, so there's nothing for it to run; this is not the same class of bug as the Prisma-script breakage described below, it's an explicit placeholder pending a baseline migration.

## Fixes applied to bring this template's Node-era pipeline in line with the proven zynkosi-tech pipeline

These fixes predate the .NET migration and describe the Node/Prisma pipeline as it existed then. Kept as history — several of the underlying files (Prisma scripts, `turbo.json`'s `globalEnv`, jest coverage scripts) no longer exist post-migration, so don't use this section as a guide to the current state; see the sections above for that.

### 1. `docker-compose.yaml` still built from source

**Symptom:** Root `docker-compose.yaml` had `build:` blocks pointing at each `Dockerfile`, contradicting this template's own `deployment-coolify` agent doc, which already described a GHCR-image, pull-only architecture.

**Fix:** Replaced every service's `build:` block with `image: ghcr.io/node-mono-repo-template/<service>:main` + `pull_policy: always`. Images are now built exclusively by `continuous-integration.yml`'s `docker-build` job.

### 2. Backend Dockerfiles missing `CI=true`, `DATABASE_URL` placeholder, and `HEALTHCHECK`

**Symptom:** `.claude/rules/docker.md` mandated `ENV CI=true` in every builder stage and a `HEALTHCHECK` in every Dockerfile, but the four backend Dockerfiles (Node-era) had neither, and had no build-time `DATABASE_URL` placeholder for Prisma's config loader (which threw eagerly at `prisma generate` time if the var was absent).

**Fix:** Added `ENV CI=true` to all four backend builder stages, `ENV DATABASE_URL="postgresql://placeholder:placeholder@localhost:5432/placeholder"` to the three DB-dependent ones (`admin-api`, `customer-api`, `schedule-api` — `api-gateway` has no database dependency), copied `tsconfig.base.json` into the build context, and added a `HEALTHCHECK` hitting each service's own liveness endpoint. All of this is moot now — the four Dockerfiles are multi-stage .NET builds (`dotnet restore`/`publish`) with no CI/DATABASE_URL build-time dependency at all.

### 3. `migrate-deploy.sh` — fragile prisma resolution

**Symptom:** The script assumed `node_modules/.bin/prisma` was always hoisted to the top level and never passed `--config`, even though `common/database/prisma.config.ts` existed and Prisma 7's config-based CLI needed it.

**Fix (superseded):** Originally rewrote the script to dynamically resolve the prisma binary, schema, and config path. That script (and its `migrate.sh` sibling) has since been deleted along with the rest of `common/database`'s Node source — see the `migrate` service's no-op state, above.

### 4. `turbo.json` missing `globalEnv`

**Symptom:** Even with `DATABASE_URL` set at the job level in CI, `typecheck`/`build`/`test` could still fail with `PrismaConfigEnvError: Cannot resolve environment variable: DATABASE_URL` — Turborepo 2's default `envMode: "strict"` silently dropped env vars from task subprocesses unless declared in `globalEnv`.

**Fix (superseded):** Added `"globalEnv": ["DATABASE_URL"]` to `turbo.json`. Removed post-migration — no pnpm-managed package reads `DATABASE_URL` anymore (EF Core reads it via the .NET options pattern, not `IConfiguration` inside a Node process).

### 5. `test:coverage` didn't exist in any `package.json`

**Symptom:** `turbo.json` had always defined a `test:coverage` task, and CI's `test` job called `pnpm test:coverage`, but no package actually implemented that script — `turbo run test:coverage` silently matched zero packages and no-op'd successfully. **No jest test had ever actually gated CI.**

**Fix:** Added `"test:coverage": "jest --coverage"` to the four backend services (`admin-api`, `api-gateway`, `customer-api`, `schedule-api`) — those packages no longer exist. The equivalent gap now exists on the .NET side (see "Backend test coverage — a known gap", above) and is unresolved, not fixed by analogy.

### 6. `release-please-config.json` / `.release-please-manifest.json` missing

**Symptom:** `version-control.yml` references `config-file: release-please-config.json` and `manifest-file: .release-please-manifest.json`, but neither file existed at the repo root — the workflow would fail on first run.

**Fix:** Added both files at the repo root. Still current — this fix wasn't stack-specific.

### 7. Three separate workflows re-running the same install+build+test cycle

**Symptom:** `production-build-and-push.yml` triggered independently off its own `push: branches: [main]`, with no dependency on `continuous-integration.yml` — a commit that failed lint/typecheck/test on `main` could still be built and deployed. `sonarcloud.yml` duplicated another full build+test cycle just to produce a coverage report.

**Fix:** Folded both into `continuous-integration.yml` as `docker-build`/`deploy` (gated behind `quality-gate`) and `sonarcloud` jobs, reusing the same `test` job's coverage artifacts instead of re-running the suite. Still current.

## Docker builds and env vars — what needs a placeholder and what doesn't

For the frontend (`admin-web`, `customer-web`): if a new `VITE_*`/`NEXT_PUBLIC_*` var is added, it needs to be declared as a build `ARG` in the Dockerfile and passed through `docker-build`'s `build-args` in `continuous-integration.yml`, matching the existing `VITE_ADMIN_API_BASE_URL`/`NEXT_PUBLIC_CUSTOMER_API_BASE_URL` pattern.

For the backend (all four ASP.NET Core services): there is no build-time env var dependency at all — every `<Service>Options` value is read from `IConfiguration` at runtime via the options pattern (`<Service>OptionsFactory.Load`), never baked into the image. Nothing needs a Docker build-arg or placeholder on the backend side; this whole category of Prisma-era gotcha (§2 above) doesn't apply anymore.
