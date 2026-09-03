---
name: database-migrations
description: Use when writing or modifying EF Core migrations, deciding on safe zero-downtime migration patterns, planning a backfill, or reviewing the migration execution strategy for CI/CD. Trigger on "migration", "add a column safely", "rename a column/table", or "backfill".
tools: Read, Edit, Write, Bash, Grep, Glob
model: inherit
---

## Before anything else — this repo has no EF Core migrations yet

Read `ef-core.md`'s "Migrations and seeding" section first. The live Postgres schema is still the one Prisma originally created; the .NET port points `AppDbContext` at it with **no new DDL** during the overlap, and `schema.prisma` itself has been deleted. The first EF Core migration anyone runs against this database must be a **baseline** (`dotnet ef migrations add InitialCreate`, then hand-edit the generated `Up()`/`Down()` to no-ops, or use `--ignore-changes` if the EF Core version supports it) — never let the first migration try to `CREATE TABLE` things that already exist. Everything below describes the *ongoing* migration discipline once that baseline is in place.

## Two commands — know when to use each

| Command | When | What it does |
|---|---|---|
| `dotnet ef migrations add <Name>` | Local development only | Creates a new migration file (`Up()`/`Down()` C# classes) — does **not** apply it |
| `dotnet ef database update` | Local dev, staging, production | Applies pending migrations |

Both require `--project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/<any-service>/src`. Neither should be run by this agent without being explicitly asked — the developer runs migrations, per the project's non-negotiable rules.

## Migration file discipline

Migration files (under `common/DotNetMonoRepoTemplate.Database/Migrations/` once they exist) are immutable once merged to `main` — never edit one that's been applied anywhere. Each migration is atomic, one logical change. Names are descriptive: `AddProductsTable`, `AddSlugToProducts`, `DropLegacySessions`. Migrations stay backward compatible with the previous code version — the running app must survive the migration before the new app version deploys.

## Zero-downtime patterns

**Adding a column (safe)**: add the property to the entity, let `dotnet ef migrations add` generate the `ADD COLUMN`. The existing (not-yet-redeployed) app version ignores the new column since EF Core only touches columns its model knows about.

**Making a column NOT NULL (two migrations)**: never add a `NOT NULL` constraint without a default on a table with existing rows in one step. Migration 1 — add as nullable (`public string? Slug { get; set; }`). Run a backfill (see below). Migration 2 (after backfill) — change to required (`public required string Slug { get; set; }`) and let EF Core generate the `ALTER COLUMN ... SET NOT NULL`.

**Renaming a property/column (never directly)**: a direct rename in EF Core generates `DROP COLUMN` + `ADD COLUMN` by default unless you explicitly tell it it's a rename — either way, a same-deploy rename breaks the running (old) app version immediately if it isn't redeployed atomically. Use expand-contract instead: Phase 1 add the new property/column and backfill-copy data from the old one; Phase 2 deploy the app version reading the new property; Phase 3 drop the old column in a follow-up migration once confirmed nothing references it.

**Renaming a table**: same pattern — add the new entity/table, dual-write or copy, migrate reads, remove the old table. Never a one-step `RenameTable` migration op in a system with rolling deploys.

**Dropping a column or table**: only after confirming no running code references it. Deploy a version removing all references first, then drop in the next migration.

**Adding an index on a large table**: EF Core's default `CreateIndex` migration op takes a lock equivalent to Postgres's standard `CREATE INDEX` (`ACCESS EXCLUSIVE` during the build). For a table exceeding roughly 1M rows, hand-edit the generated migration to use `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ...")` instead of the generated `migrationBuilder.CreateIndex(...)` call — and note that `CREATE INDEX CONCURRENTLY` cannot run inside a transaction, so the migration also needs `[SuppressMessage]`/the appropriate EF Core mechanism to disable the implicit per-migration transaction for that specific migration (check the current EF Core version's documented way to do this before writing the migration — this detail is version-sensitive and worth verifying against a compiler, not assumed from memory).

## Backfill pattern

1. Add the column as nullable in the migration
2. Add a backfill method to a service in `schedule-api` (or wherever the corresponding cron/job infrastructure lives — see `DotNetMonoRepoTemplate.Queue`'s `JobDispatcher` if the backfill should run as a dispatched job rather than a one-off script)
3. Process rows in batches of 500 using cursor-based iteration (`.Where(x => x.Id.CompareTo(cursor) > 0).OrderBy(x => x.Id).Take(500)`, not `.Skip(n).Take(500)` — offset pagination gets slower and can skip/duplicate rows under concurrent writes as the table changes underneath it)
4. Make the job idempotent — safe to re-run if it fails midway (only touch rows where the target column is still null)
5. Follow-up migration makes the column required after backfill completes

```csharp
public async Task BackfillSlugsAsync(CancellationToken cancellationToken)
{
    string? cursor = null;
    while (true)
    {
        var query = _db.Products.Where(p => p.Slug == null);
        if (cursor is not null)
        {
            query = query.Where(p => string.Compare(p.Id, cursor) > 0);
        }
        var batch = await query.OrderBy(p => p.Id).Take(500).ToListAsync(cancellationToken);
        if (batch.Count == 0)
        {
            break;
        }
        foreach (var product in batch)
        {
            product.Slug = GenerateSlug(product.Name);
        }
        await _db.SaveChangesAsync(cancellationToken);
        cursor = batch[^1].Id;
    }
}
```

## Migration execution strategy for this project

This project deploys via Coolify on a self-hosted VPS, not Kubernetes. Migrations should execute via the API application's Coolify post-deployment command — see `deployment-coolify.md` for the full mechanism this needs to be rebuilt around now that it's `dotnet ef database update` instead of `prisma migrate deploy` (the compose file's `migrate` one-shot service and the post-deploy command both still reference the Prisma command as of this writing — that's tracked Phase 6 follow-up work, not something already wired for .NET). Never run migrations inside `Program.cs` on startup — multiple replicas would race, same reasoning as the Node era. If this project ever migrates to Kubernetes, migrations would instead run as a Job (init-container pattern) completing before new Pods start — but that isn't the current setup.

## CI/CD migration checklist

A migration file exists for every schema change — no direct DB edits. New required (`NOT NULL`) columns have a default or are added nullable with a backfill plan. Large table index additions use `CREATE INDEX CONCURRENTLY` via a hand-edited migration. Column/table renames follow expand-contract, never a direct EF Core rename op. `dotnet ef database update` runs via the Coolify post-deploy command before traffic reaches the new version (once that command is updated for .NET — see above).
