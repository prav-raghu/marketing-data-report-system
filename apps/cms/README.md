# cms

ASP.NET Core (.NET 10) CMS built on [Piranha CMS](https://piranhacms.org/), replacing the Strapi scaffold this app previously held. Piranha ships its own Manager admin UI (Razor Pages-based — this is a Piranha vendor requirement, not a departure from this repo's "Minimal APIs only" rule, which is scoped to `apps/backend/*` in `.claude/rules/backend.md`; `apps/cms` is a separate top-level app in the folder structure and follows Piranha's own conventions).

## Status

This is a from-scratch scaffold, not a port — the Strapi instance it replaces had zero custom content types, zero migrations, and zero cross-references from any other app in this repo (verified before deleting it). There was no content model or data to carry over. This ships one placeholder page type (`StandardPage`, block-based) so the Manager UI has something usable on first login; real content types are added the same way as any Piranha project — a new class under `Models/` decorated with `[PageType]`/`[PostType]`, registered in `Program.cs`'s `PageTypeBuilder`.

**Lower confidence than the rest of this migration**: this project was built without a working `dotnet` SDK to compile-check against, and without live access to piranhacms.org's docs (both blocked in the sandbox that wrote this) — verified only against real NuGet package names/versions (`api.nuget.org`), not a freshly-fetched reference implementation. Treat `Program.cs`'s `AddPiranha`/`UsePiranha` builder chain — especially `options.UseEF<PostgreSqlDb>(...)` and `options.UseIdentityWithSeed<IdentityPostgreSQLDb>(...)` — as the most likely spot to need a small adjustment once someone runs `dotnet build` against it for the first time. Everything else (project structure, options pattern, Dockerfile, env vars) follows the same conventions as the four backend services and should not need special scrutiny.

## Manager UI

`https://<host>/manager` — `UseIdentityWithSeed` creates a default seed admin account on first run (Piranha's own default, not something this project overrides — check Piranha's current docs for the exact seeded credentials, and change the password immediately after first login in any non-local environment). There is no `.claude/agents/` entry for CMS-specific work yet — treat `backend-service`-style scrutiny (options pattern, no hardcoded secrets, structured logging) as the baseline until a dedicated agent doc exists.

## Local development

```bash
cp .env.example .env
dotnet run --project src/Cms.csproj
```

Requires a reachable Postgres via `DATABASE_URL` — Piranha manages its own tables in the same database via its EF Core provider, using a separate migration history from `common/DotNetMonoRepoTemplate.Database`'s own tables (Piranha owns its schema entirely; this project's `AppDbContext` and Piranha's Postgres provider do not share entities).

## Environment variables

See `.env.example`. `DATABASE_URL`/`PORT`/`NODE_ENV` are read through `CmsOptionsFactory` (the same options-pattern convention as every backend service) — never read `IConfiguration`/environment variables directly outside that factory.
