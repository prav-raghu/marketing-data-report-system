# automation

ASP.NET Core (.NET 10) Minimal API service hosting [Elsa Workflows](https://elsaworkflows.io/) 3.7.1, replacing the n8n Docker/Kubernetes scaffold this app previously held.

## Status

This is a from-scratch scaffold, not a port — the n8n setup it replaces (`compose/`, `k8s/`, per-project instance provisioning scripts) had zero real workflow definitions checked in (`workflows/` was just a `.gitkeep`) and zero cross-references from any other app in this repo (verified before deleting it). There was no automation logic to carry over.

**Headless API only, no Elsa Studio** — Elsa ships an optional Blazor-based visual designer (`Elsa.Studio.*` packages), but this repo's frontend stack is React/Next.js; adding Blazor as a second UI framework just to design workflows that don't exist yet was judged not worth the complexity. `Elsa.Workflows.Api` exposes the same REST surface Elsa Studio itself talks to, so a designer UI (Elsa Studio, or a custom React one) can be added later without touching this service. This is a deliberate architecture decision, not an oversight — revisit it if/when a real workflow-authoring need shows up.

**Lower confidence than the rest of this migration**: this project was built without a working `dotnet` SDK to compile-check against, and without live access to docs.elsaworkflows.io (blocked in the sandbox that wrote it) — verified only against real NuGet package names/versions (`api.nuget.org`), not a freshly-fetched reference implementation. Treat `Program.cs`'s `AddElsa`/`UseWorkflowManagement`/`UseWorkflowRuntime`/`UseWorkflowsApi`/`UseWorkflows` chain as the most likely spot to need a small adjustment once someone runs `dotnet build` against it for the first time — in particular, the exact `UseEntityFrameworkCore`/`UsePostgreSql` method names and whether `app.UseWorkflows()` (the HTTP-trigger middleware) needs to run before or after `app.UseWorkflowsApi()`. Everything else (project structure, options pattern, Dockerfile, env vars, metrics/health wiring) follows the same conventions as the four backend services and should not need special scrutiny.

## No workflows exist yet

There is nothing to run out of the box — Elsa's workflow-management API lets workflows be defined via its REST API (or, once added, a designer UI) and persisted to Postgres via `Elsa.Persistence.EFCore.PostgreSql`. `Elsa.Http` and `Elsa.Scheduling` are included so HTTP-triggered and cron-scheduled workflows (n8n's two most common trigger types) are available from day one.

## Local development

```bash
cp .env.example .env
dotnet run --project src/WorkflowApi.csproj
```

Requires a reachable Postgres via `DATABASE_URL` — Elsa manages its own tables in the same database via its EF Core provider, using a separate migration history from `common/DotNetMonoRepoTemplate.Database`'s own tables.

## Environment variables

See `.env.example`. `DATABASE_URL`/`PORT`/`NODE_ENV`/`CORS_ORIGIN` are read through `WorkflowApiOptionsFactory` (the same options-pattern convention as every backend service) — never read `IConfiguration`/environment variables directly outside that factory.
