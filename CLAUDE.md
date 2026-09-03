# zynkosi-tech-dot-net-mono-repo-template

## Stack split (read this first)

**Backend is ASP.NET Core / C# (.NET 10).** Every service under `apps/backend/*`, every shared library under `common/*`, `apps/cms` (Piranha CMS), and `apps/automation` (Elsa Workflows) was migrated off Node/Fastify/Prisma/Strapi/n8n — see `documentation/dotnet-migration-plan.md` for the full record. `apps/cms` and `apps/automation` were both empty scaffolds when ported (no custom Strapi content types, no real n8n workflows, zero cross-references from any other app), so Phases 8–9 were a clean scaffold swap, not a data/logic migration — unlike the four backend services, neither was built against a working `dotnet` SDK or live vendor docs (both blocked in the sandbox that wrote them), so treat their framework-integration code (Piranha's `AddPiranha`/`UsePiranha` builder chain, Elsa's `AddElsa`/`UseWorkflowManagement`/`UseWorkflowRuntime` setup) as needing a real `dotnet build` pass before it's trusted, more so than the rest of this migration — see each app's own README for the specific lines flagged.

**Frontend is Blazor now too.** `admin-web` (React/Vite) and `customer-web` (Next.js) were retired in Phase 4 of a second migration — see the security-audit-and-Blazor-migration plan referenced in recent commit history for the full record — replaced by `apps/frontend/admin-web` (Blazor WebAssembly Standalone, C#/.NET 10) and `apps/frontend/customer-web` (Blazor Web App with hybrid render modes, C#/.NET 10). Both were built and cut over without a working `dotnet` SDK in the sandbox that wrote them (same constraint as `apps/cms`/`apps/automation` above) — treat all Blazor framework-integration code (the `InteractiveWebAssembly` render-mode wiring, the two-project split in `customer-web`) as needing a real `dotnet build`/`dotnet publish` pass before it's trusted, more so than any other part of this repo. See `.claude/rules/frontend-blazor.md` for the conventions and real architectural findings from building both, and each app's own README for what's proven vs. still open. `customer-mobile` was Ionic/Capacitor (TypeScript) through Phase 4; in Phase 5 it was replaced by a .NET MAUI Blazor Hybrid app at the same path (`apps/mobile/customer-mobile`, C#/.NET 10) — it is not wired into anything yet (not in `DotNetMonoRepoTemplate.sln`, no Dockerfile/CI, needs the `maui` workload the rest of the solution doesn't require) and is the least-verified part of this entire migration; see its own README and `.claude/rules/mobile.md` before treating it as more than a sketch.

There is no longer a shared runtime type package between backend and frontend — `common/types` (TypeScript) no longer exists; its C# replacement, `DotNetMonoRepoTemplate.Types`, is backend-only. Frontend apps declare their own local TypeScript types for wire shapes.

## Instruction precedence

When guidance conflicts, resolve in this order:

1. The user's explicit request
2. Security/permission constraints in `.claude/settings.json`
3. Current repository code and committed configuration (e.g. `docker-compose.yaml`, `DotNetMonoRepoTemplate.sln`, `AppDbContext.cs`) — a written example never overrides what's actually committed
4. Path-specific files in `.claude/rules/`
5. The selected subagent in `.claude/agents/`
6. This file
7. Deep-dive reference docs in `.claude/instructions/`
8. Legacy commands in `.claude/commands/` — thin entry points that delegate to an agent; treat any command content that contradicts a higher-precedence source as stale, not authoritative

If a command, agent, and rule genuinely disagree instead of one being simply out of date, say so and ask rather than silently picking one.

## Subagents (available; delegate when a task clearly matches a description)

Descriptions below help Claude choose the right delegate — they are not a deterministic routing table. When a task matches one clearly, use it; when it's ambiguous, pick the closest fit or ask.

| Subagent | Scope |
|---|---|
| `full-stack-orchestrator` | Builds spanning database + backend + frontend together |
| `backend-service` | General ASP.NET Core Minimal API service work (endpoints, middleware, DI-registered services, options pattern) |
| `api-builder` | Generating full CRUD layers from an existing EF Core entity |
| `jwt-security` | Scaffolding/reviewing auth routes (login, logout, refresh, MFA) — token blacklist, refresh rotation, the per-user `minIat` "logout everywhere" marker. Apply alongside `api-builder` for auth domains |
| `domain-modeler` | Designing new EF Core entities from business requirements |
| `relational-database` | EF Core operations — migrations, seeding, naming, database issues |
| `database-migrations` | Zero-downtime EF Core migration patterns, backfills, CI migration checklist |
| `audit-log` | Audit trail pattern for state-changing operations |
| `enterprise-scale` | Cross-cutting 1M+ concurrent user patterns — cache, queue, pagination |
| `frontend-react` | **Retired** — admin-web (React + Vite SPA) was decommissioned in Phase 4 of the Blazor migration; kept only so the name doesn't dangle, see `.claude/rules/frontend-blazor.md` for admin-web instead |
| `frontend-nextjs` | **Retired** — customer-web (Next.js, SEO/SSR) was decommissioned in Phase 4 of the Blazor migration; kept only so the name doesn't dangle, see `.claude/rules/frontend-blazor.md` for customer-web instead |
| `frontend-page-builder` | Generating full page/component/hook layers for a domain — currently still written for React/Next.js; needs a Blazor-aware rewrite before real use post-cutover |
| `mobile` | .NET MAUI Blazor Hybrid customer-mobile app (C#, since Phase 5) — replaced the former Ionic + Capacitor app at the same path |
| `common-packages` | Shared `common/DotNetMonoRepoTemplate.*` C# class libraries |
| `new-service-scaffold` | Scaffolding a brand new ASP.NET Core service, frontend app, or common library |
| `rbac` | Permissions, `RequirePermissionsAttribute` guards, role-to-permission mapping |
| `webhook-events` | Outbound webhooks and the internal event bus |
| `feature-flags` | DB-backed feature flag store and evaluation |
| `infrastructure` | Terraform, Kubernetes (future), Docker Compose, NGINX |
| `vps-bootstrap` | One-time fresh-VPS setup — prerequisite to deployment-coolify |
| `deployment-coolify` | Canonical deploy path: Coolify, multi-stage .NET Dockerfile builds, managed Postgres/Redis, DNS |
| `testing` | Unit/integration tests, xUnit config, EF Core in-memory/Testcontainers fixtures, factories |
| `csharp-standards` | Backend type-safety/nullability review outside a full code review — no `dynamic`, no unjustified `object`, no suppressed nullable warnings |
| `typescript-standards` | **Retired** — `customer-mobile` moved off TypeScript to .NET MAUI Blazor Hybrid (C#) in Phase 5; there is no TypeScript app left anywhere in this repo, kept only so the name doesn't dangle — use `csharp-standards` for `apps/mobile/*` and `apps/frontend/*` alike now |
| `code-review` | Full quality/security audit |

For anything not covered by a subagent above, read the relevant file in `.claude/instructions/` before writing code.

## Model selection when delegating to a subagent

Every subagent's `model:` frontmatter is `inherit` unless noted below — it runs on whatever model this session is running on. The `Agent` tool also accepts a per-call `model` override that beats the agent's own frontmatter; use it deliberately to control cost, not by default.

**Override down to `claude-haiku-4-5-20251001` only when *all* of these hold:**
- The task mirrors an existing, unambiguous pattern already in this codebase (e.g. "add a 6th CRUD entity shaped exactly like the other 5") — not a first-of-its-kind design decision.
- Nothing security-, auth-, RBAC-, payment-, or PII-adjacent is being written or touched.
- A mistake would be caught by `dotnet build`/analyzers/tests before merge — not the kind of subtle logic error that slips past mechanical checks and only shows up as a real bug later (an N+1 query, a missing `.Include()`, a silently-swallowed exception — none of these fail a build).
- The blast radius is one file or one entity, not a shared `common/*` library or a cross-cutting concern.

`new-service-scaffold` and `testing` (for straightforward, already-understood service/controller test-writing — not for designing a test strategy for something novel) default to Haiku in their own frontmatter for exactly this reason: they're closer to templating than judgment. That default is a starting point, not a floor — bump either back to Sonnet for an unusually complex instance of their normal work.

**Never override down — keep Sonnet (or the session's own model) — for:** `jwt-security`, `rbac`, `database-migrations`, `domain-modeler`, `code-review`, `csharp-standards`, `deployment-coolify`, `infrastructure`, `vps-bootstrap`, `enterprise-scale`, `full-stack-orchestrator`, `audit-log`, `webhook-events`, `common-packages`, `relational-database`. These carry either security consequences, wide blast radius, or genuine architectural judgment that a mistake won't surface as a clean build failure — it surfaces as a production incident or a silent vulnerability (or, for the query-shape mistakes this stack is prone to, a service that works in dev and falls over under load). `code-review` in particular is the backstop for everything else in this list; downgrading the backstop defeats the point of having one.

Everything else (`api-builder`, `frontend-page-builder`, `mobile`, `feature-flags`, `backend-service`) is task-dependent — judge the specific request against the four bullets above each time rather than a fixed per-agent answer.

This list is a starting point, not a settled policy — if a Haiku-delegated task comes back needing real rework, that's a signal to tighten these criteria (or move that agent to the "never override" list), not to push through it.

## Deployment — Coolify is canonical

This project deploys to a self-hosted Hetzner VPS via Coolify. `vps-bootstrap` runs once on a fresh server (and installs Coolify); `deployment-coolify` covers everything after that (multi-stage .NET Dockerfiles, per-application config, env vars, managed Postgres/Redis, git-push auto-deploy, DNS). No other deployment path (Render, raw SSH, Kamal) is used for this project.

## Skills (invoke with /name, or loaded by Claude when a description clearly matches)

| Skill | When to use |
|---|---|
| `/ui-ux-pro-max` | Before building any frontend page or component — design system lookup, color, typography, UX patterns |
| `/build-page` | Build a complete frontend page end-to-end with design intelligence baked in |
| `/security-review` | Audit code for auth gaps, injection risks, and secrets before merge |
| `/code-review-skill` | Full quality review: types, naming, security, form validation coverage |
| `/seo-optimization` | Audit/improve SEO for `customer-web` — metadata, structured data, sitemaps, Core Web Vitals |

`/build-page` and `/seo-optimization` predate the Blazor migration and are written for React/Next.js — they need a Blazor-aware pass (page-builder patterns, `<PageTitle>`/`<HeadContent>` instead of Next's `Metadata` export) before real use against `admin-web`/`customer-web`; treat their current content as a reference for intent, not literal instructions to follow. `/ui-ux-pro-max`, `/security-review`, and `/code-review-skill` are stack-agnostic enough to still apply as-is.

### Meta — Skill Creator

`skill-creator` (global, not project-local) scaffolds a new reusable skill from a plain-English description — testing, packaging, and description-tuning included. Reach for it when a multi-step task pattern has come up 3+ times in this project and would benefit from a repeatable, invocable skill instead of re-explaining it each time (mirrors the existing skills above, which were built the same way). It's the capability-uplift complement to the domain-specific skills in the table: those encode *this project's* conventions, `skill-creator` is how you add the next one without hand-authoring `SKILL.md` frontmatter from scratch.

## Commands (legacy, still work)

`/add-endpoint`, `/add-entity`, `/add-service`, `/add-pages`, `/design-database`, `/add-tests`, `/review`, `/build-system`, `/deploy-coolify`, `/init-project`, `/provision-infrastructure`, `/request-logging`, `/strapi-setup`

Per the precedence rules above, these are thin entry points that delegate to the agents in the table — the agents are authoritative on stack specifics (C# vs. TypeScript), not the command files themselves. `/strapi-setup` is retired — `apps/cms` is Piranha CMS (C#) now, not Strapi; the command file is kept only so the slash-command doesn't dangle, and points at `apps/cms/README.md` instead of scaffolding Strapi content types.

## Memory — Claude Mem (replaces Serena)

Serena (LSP-backed code navigation MCP server) has been dropped from this project — no `.mcp.json` and no `.serena/` config are committed anymore. In its place, use **Claude Mem** for cross-session memory: it hooks into the session lifecycle (`SessionStart`, `UserPromptSubmit`, `PostToolUse`, `Stop`, `SessionEnd`), summarizes what happened, and stores it in a local SQLite + vector-search store so decisions and context persist between sessions instead of vanishing when a session ends.

This is a per-developer, one-time install — Claude does not run it for you:

```bash
npx claude-mem install
```

Local dashboard (session history, memory search): `http://localhost:37777`. Wrap anything session-specific and sensitive (API keys, customer data) in `<private>...</private>` in a prompt to exclude it from what gets stored. Treat Claude Mem as the project's answer to "does this session remember what the last one decided" — reach for it instead of re-explaining architectural decisions each session.

## UI/UX skill setup (one-time global install)

```bash
npm install -g ui-ux-pro-max-cli
uipro init --ai claude --global
```

Requires Python 3.x. After install, the `/ui-ux-pro-max` skill has access to 50+ design styles, 161 color palettes, 57 font pairings, and stack-specific guidance for React, Next.js, and React Native.

## Non-negotiable rules

### Backend (C# / ASP.NET Core)

- ASP.NET Core Minimal APIs (.NET 10) — no MVC controllers, no NestJS-style decorators-as-framework
- FluentValidation only for backend request validation — never Data Annotations, never Zod on the backend
- Nullable reference types are `enable`d solution-wide and nullable warnings are errors (`Directory.Build.props`) — no `#pragma warning disable`, no unjustified `!` null-forgiving operator, no `dynamic`, no `object` where a concrete type or generic works
- DTOs are `sealed record` types only — never mutable classes, never DTOs-as-entities
- No comments in code
- No hardcoded secrets — all secrets via environment variables, read through each service's `<Service>Options` class (never `Environment.GetEnvironmentVariable` scattered through business logic)
- No `Console.Write`/`Console.WriteLine` anywhere — structured logging only, via the shared `DotNetMonoRepoTemplate.Logging.Logger` (Serilog-backed)
- No N+1 queries: a `foreach`/loop that issues an EF Core query per iteration is a defect, not a style nit. Load what you need with `.Include()`/projection *before* the loop; if you must fetch per-item inside a loop because of genuine per-item I/O (an HTTP call, e.g. `WebhookDeliveryService.DeliverWebhookAsync`), that's fine — but never re-query for an entity the caller already has loaded and tracked
- Folder structure is immutable — do not create new top-level folders
- `common/` for all shared C# libraries — not `packages/` or `libs/`. All backend libraries are scoped as `DotNetMonoRepoTemplate.[Name]` (C# namespace, not an npm scope) and referenced via `<ProjectReference>`, never copy-pasted
- All entities, DTOs, records, and constants in their own files (one type per file — see any file under `common/DotNetMonoRepoTemplate.Types/` for the pattern)
- Match existing coding style — explicit methods with real bodies for business logic; expression-bodied members (`=>`) only for trivial one-liners (simple property getters, pure delegations)
- Do not run database migrations (`dotnet ef migrations add`/`dotnet ef database update`) — the developer runs them unless explicitly asked
- Do not run git operations — the developer runs them unless explicitly told to
- Service layer of every API needs automated unit tests under `apps/backend/<service>/tests/` (xUnit, mirroring the `Services/` folder structure — see `testing.md`)
- Dockerfiles live at each app's own root: `apps/backend/<service>/Dockerfile` (multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` build stage → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime stage), `apps/frontend/<app>/Dockerfile`
- Root `docker-compose.yaml` is the single Coolify deployment stack
- Before marking any backend task complete, run `dotnet build <service>.csproj` (or the full `DotNetMonoRepoTemplate.sln`) — zero errors and zero warnings required, since nullable warnings are errors. Nothing in this repo has been compiler-verified end-to-end yet post-migration (no .NET SDK was available in the sandbox that did the port) — treat freshly-ported code as needing this check before it's trusted, not as already-proven
- Redis connections use `REDIS_URL` only — no discrete `REDIS_HOST`/`REDIS_PORT`/`REDIS_PASSWORD` fallback anywhere in the stack
- Database connections use `DATABASE_URL` only — no discrete `DB_HOST`/`DB_PORT`
- The first `SUPER_ADMIN` account is intended to be created only through a one-time `admin-api` bootstrap route — **this route does not currently exist** in either the original Node code or the .NET port (confirmed by search before the migration touched auth). Don't invent it as a side effect of unrelated work; if asked to add it, treat it as new functionality requiring the same scrutiny as any other auth-surface change, not a "restore what was there" task
- Every entity gets the six base metadata fields (`Id`, `IsActive`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `ModifiedBy`) via the shared `AuditableEntity` base class (or `TimestampedEntity` for the narrower `Id`/`CreatedAt`/`UpdatedAt`-only case) unless it's a narrow, documented exception — see `relational-database.md`
- Dates: `DateTime`/UTC in the DB (Npgsql + `EFCore.NamingConventions` for snake_case columns), ISO 8601 on the wire in both directions (System.Text.Json's default `DateTime` serialization) — display formatting (`dd/MM/yyyy`) is a frontend-only concern via `date-fns`, unaffected by this migration
- Email fields get disposable-domain rejection in the service layer, not just FluentValidation syntax checks — see `validation-chain.instructions.md`
- Logout invalidates every active session for that user (all devices/tabs), not just the token that called logout — implemented via the per-user `token:minIat:{userId}` Redis marker; see `jwt-security.md`
- MFA (TOTP, via Otp.NET + QRCoder) is two-step at login when `TwoFactorEnabled`: password success returns a short-lived `mfaToken` (a JWT with `type: "mfa_challenge"`), not real tokens — real tokens issue only after `/auth/verify-login-mfa` passes. Never treat password-correct as login-complete for an MFA-enabled user. See `jwt-security.md`

### Frontend — `apps/frontend/*` (Blazor / C#, since Phase 4)

- Blazor WebAssembly Standalone (`admin-web`, no SEO/SSR need) or Blazor Web App with hybrid render modes (`customer-web`, SEO-critical) — no other pattern for a new `apps/frontend/*` app without discussing it first; see `.claude/rules/frontend-blazor.md` for which pattern fits a new app and the full non-negotiable list (FluentValidation on submit, DI-registered state instead of a state-management library, `HttpClient` + `Microsoft.Extensions.Http.Polly` instead of Axios interceptors, tokens in memory only, `@attribute [Authorize]` explicit on every gated page, `@onsubmit:preventDefault="true"` on every plain `<form>`)
- Nullable reference types, `sealed record` for response DTOs (request/form-bound models are the one sanctioned exception — mutable classes, since `@bind` needs a settable property), no comments in code — same rules as the backend, since this is C# now
- Tailwind v4 for styling, same CSS-variable theme tokens as the retired React apps had (don't reinvent a palette) — built via `@tailwindcss/cli` as a `pnpm build:css` step alongside `dotnet build`, since each Blazor app keeps a small Tailwind-only `package.json`
- Before marking any Blazor frontend task complete, run `dotnet build`/`dotnet test` on the app's own `.csproj`(s) — same bar as the backend, zero errors/warnings
- bUnit for component tests, mirroring xUnit conventions

### Mobile — `apps/mobile/customer-mobile` (.NET MAUI Blazor Hybrid, C#, since Phase 5)

- .NET MAUI Blazor Hybrid — replaced the former Ionic + Capacitor (TypeScript) app at this same path; not in `DotNetMonoRepoTemplate.sln` (needs the `maui` workload, which the base SDK the rest of the solution builds with doesn't have), no Dockerfile/CI yet, and the least-verified part of this entire migration (no MAUI workload, Android/iOS toolchain, or macOS host were available to the session that wrote it) — see `.claude/rules/mobile.md` and the app's own README before trusting or extending anything under it
- Nullable reference types, no comments in code, FluentValidation for forms (none exist yet in the current shell) — same rules as the rest of the C# codebase, since this is C# now
- Native device APIs go through MAUI's own abstractions (`Microsoft.Maui.Devices`, `Microsoft.Maui.ApplicationModel`, `Microsoft.Maui.Storage`), not Capacitor, which no longer exists in this repo
- Before marking any task complete, run `dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f <target>` for whichever platform target is actually buildable in your environment — if no MAUI workload/platform tooling is available, say so explicitly rather than marking the task done unverified

## Folder structure (immutable)

```text
apps/backend/        ASP.NET Core (.NET 10) Minimal API services
apps/frontend/       Blazor (.NET 10) — admin-web (WASM Standalone), customer-web (Web App, hybrid render) — React/Next.js retired in Phase 4
apps/mobile/         .NET MAUI Blazor Hybrid (C#, .NET 10) — customer-mobile — Ionic + Capacitor retired in Phase 5
apps/cms/            Piranha CMS (C#, .NET 10) — Strapi decommissioned, Phase 8 complete
apps/automation/     Elsa Workflows (C#, .NET 10) — n8n decommissioned, Phase 9 complete
common/              shared libraries only — C# class libraries (DotNetMonoRepoTemplate.*) for backend concerns
devops/              local dev Docker Compose and scripts
infrastructure/      Nginx, Terraform
documentation/       markdown docs, including dotnet-migration-plan.md (the migration's full record)
.github/             CI/CD workflows
.claude/             Claude Code configuration
  agents/            subagent definitions (delegated to by description match, not deterministic)
  commands/          legacy slash commands (single-file)
  hooks/             scripts run on tool use events
  instructions/      reference docs, read manually or by cross-reference — not auto-loaded (see note below)
  rules/             path-gated rules (auto-load when matching files enter context via `paths:` frontmatter)
  skills/             reusable skills invoked with /name
  templates/         scope and PR templates
  workflows/         dynamic multi-agent workflow scripts
```

`.claude/instructions/` predates `.claude/rules/` and is not auto-gated by Claude Code itself — any `applyTo:` frontmatter on files in that folder is informational only, not a mechanism the harness parses. Domain conventions that need to auto-load on matching files live in `.claude/rules/` instead; `.claude/instructions/` is now reserved for deep-dive reference docs (e.g. the EF Core → FluentValidation → Zod validation chain, JWT token lifecycle, OpenAPI/Swagger setup) that agents and rules link to by name.

## Package managers

Two, split by stack — don't cross them:

- **Backend + frontend + mobile (C#)**: the `dotnet` CLI, with **NuGet Central Package Management** — every package version lives once in root `Directory.Packages.props`, individual `.csproj` files reference packages by name only (no version). Internal library references use `<ProjectReference>`, never a package feed. Solution-wide settings (target framework, nullable, analyzers) live in root `Directory.Build.props`. This covers `apps/frontend/*` since Phase 4 (every Blazor app's `.csproj`(s) are in `DotNetMonoRepoTemplate.sln` alongside the backend services) — `apps/mobile/customer-mobile` is C#/.NET MAUI too since Phase 5, but is **not** in the `.sln` (it needs the `maui` workload, which the rest of the solution doesn't require), so build it directly: `dotnet build apps/mobile/customer-mobile/src/CustomerMobile.csproj -f <target>`.
- **pnpm**: no TypeScript app remains anywhere in this repo as of Phase 5. pnpm's one remaining touchpoint is Tailwind CSS builds — each Blazor app under `apps/frontend/*` and the MAUI app under `apps/mobile/customer-mobile` keeps a small `package.json` (Tailwind CLI only, no framework deps) so `pnpm --filter <app> build:css` resolves it as a normal pnpm-workspace member.

## Port assignments

| Service | Port |
|---|---|
| api-gateway | 4000 |
| admin-api | 4001 |
| customer-api | 4002 |
| schedule-api | 4003 |
| customer-web | 3000 (ASP.NET Core host, replaces the Node `output: standalone` server — same port) |
| admin-web | 80 (nginx serving the published `wwwroot`, Traefik-routed in production — same deployment shape as the retired Vite SPA); `dotnet run` dev server has no fixed port convention yet |
| customer-mobile | n/a — not deployed as a network service; a .NET MAUI Blazor Hybrid app ships through app stores, not a listening port. The retired Ionic app's Vite dev ports (5173/5174) no longer apply. |
| cms | 4005 |
| automation (workflow-api) | 4006 |

All four backend services are ASP.NET Core now; the ports are unchanged from the Node era so nothing downstream (gateway routing, frontend API base URLs, Coolify config) needed to move. The Phase 4 Blazor cutover kept the same two frontend ports (3000, 80) for the same reason — nothing downstream needed to move. `cms`/`automation` are new port assignments, not carried over — Strapi and n8n used their own default ports (1337, 5678) which never needed to match this table.

## Using this as a template — project name substitution

When forking for a new project, replace `node-mono-repo-template` with the project slug everywhere, including `customer-mobile`'s `package.json` name field and the GHCR image names in CI/`docker-compose.yaml` (`ghcr.io/node-mono-repo-template/<service>:main`). For the C# side — which now includes `apps/frontend/*` and `apps/mobile/customer-mobile` as well as `apps/backend/*` and `common/*`, since Phase 4 and Phase 5 respectively — replace the `DotNetMonoRepoTemplate` namespace/library prefix (`DotNetMonoRepoTemplate.Types`, `DotNetMonoRepoTemplate.Database`, `AdminWeb`, `CustomerWeb`, `CustomerMobile`, etc., and the `DotNetMonoRepoTemplate.sln` filename itself) with your project's PascalCase name — note `CustomerMobile`'s `.csproj` isn't in the `.sln` (see "Package managers" above), so rename it directly. This covers `package.json` name fields, `pnpm-workspace.yaml`, every `.csproj`/`.sln` file, `CLAUDE.md`, every file under `.claude/agents/`, every file under `.claude/commands/`, and every file under `.claude/instructions/`.

After substitution, fill in `infrastructure/terraform/*/environments/*.tfvars` and each `apps/backend/*/.env`, then replace this section with project-specific notes.

### Region-specific defaults baked into agent instructions — check these on fork

This template was built for a South Africa–based project, and several "non-negotiable" patterns encode that region rather than being truly generic:

- **Phone validation regex** `^(\+27|0)[6-8][0-9]{8}$` — appears in `api-builder.md` (now as a FluentValidation `.Matches()` rule), `validation-chain.instructions.md`, and `build-page/SKILL.md`; the same pattern applies to any FluentValidation rule written for a Blazor form post-Phase-4, and to `rules/mobile.md`'s form conventions. Swap this for the target country's phone format before generating any forms.
- **SMSPortal** (`SMSPORTAL_CLIENT_ID`/`SMSPORTAL_API_SECRET`) — SA-specific SMS provider, ported to `DotNetMonoRepoTemplate.Sms`'s `SmsService` as a raw `HttpClient` REST call, wired into the env-var contract in `deployment-coolify.md` and `infrastructure.md`. Swap for a provider available in the target region.
- **Hetzner EU (Falkenstein) / ZAR-adjacent defaults** in `vps-bootstrap.md` and `deployment-coolify.md` are reasonable starting points but not mandatory — pick a region close to the actual user base.

None of these are structural — swap the values, not the patterns they're embedded in.
