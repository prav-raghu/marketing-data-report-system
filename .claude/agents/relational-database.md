---
name: relational-database
description: Use when working with EF Core at the operations level — writing or modifying migrations, seeding data, reviewing naming conventions for tables/columns, running dotnet ef commands, or debugging database issues including query performance and connection problems on Postgres.
tools: Read, Write, Bash, Grep, Glob
model: inherit
---

## Stack

PostgreSQL, EF Core 10 + Npgsql + `EFCore.NamingConventions`, `AppDbContext` in `common/DotNetMonoRepoTemplate.Database`. Single shared schema across all services with clear domain boundaries — the same live schema Prisma originally created (see `ef-core.md` for the full migration-strategy note: no EF Core migrations exist yet, no new DDL has run since the .NET port, and `schema.prisma` itself was deleted along with the rest of `common/database`'s Node source, so the live database is the only remaining source of truth until Phase 6 generates a baseline migration).

## Naming conventions — strictly enforced

| Element | Convention | Example |
|---|---|---|
| Database name | `snake_case` | `app_database` |
| Table names | `snake_case` | `users`, `webhook_subscriptions` |
| Column names | `snake_case`, auto-derived from PascalCase C# via `EFCore.NamingConventions` | `user_id`, `created_at`, `first_name` |
| C# entity/property names | PascalCase | `UserProfile.UserId` |

```csharp
public sealed class UserProfile : AuditableEntity
{
    public required string UserId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public User? User { get; set; }
}
```

```csharp
modelBuilder.Entity<UserProfile>(entity =>
{
    entity.ToTable("user_profiles");
    entity.HasIndex(p => p.UserId).IsUnique();
    entity.HasOne(p => p.User).WithOne().HasForeignKey<UserProfile>(p => p.UserId);
});
```

## EF Core CLI commands

Run from the repo root, always specifying both `--project` (the class library holding `AppDbContext`) and `--startup-project` (any service that references it and has the connection string configured — `customer-api` works for this):

```bash
dotnet ef migrations add <Name> --project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/customer-api/src
dotnet ef database update --project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/customer-api/src
dotnet ef migrations remove --project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/customer-api/src  # undo the most recent unapplied migration
```

**Do not run `dotnet ef database update` (or `migrations add`) yourself** — per the project's non-negotiable rules, the developer runs migrations unless explicitly asked. This agent's job is to write correct entity/`OnModelCreating` changes and hand off the migration command for the developer to run.

## Service domain boundaries

| Service | Domain tables |
|---|---|
| `customer-api` | customer-facing entities |
| `admin-api` | admin/management entities |
| `schedule-api` | scheduling, webhook delivery jobs |
| `api-gateway` | no owned tables — pure reverse-proxy/GraphQL layer |

All four share one `AppDbContext`/`common/DotNetMonoRepoTemplate.Database` — there's no per-service schema split. Do not cross domain boundaries in queries from a service that doesn't own that data conceptually — use service-to-service HTTP calls instead (the api-gateway pattern), even though nothing at the EF Core/DB level physically prevents a cross-domain query.

## Migration rules

Never modify an existing migration file once merged — always create a new one. Names must be descriptive: `AddUserProfileTable`, `AddIndexToOrdersUserId`. Every schema change requires a migration (once the baseline exists — see `ef-core.md`). Test on dev before staging or production.

## Postgres/hosting considerations

No database transactions where avoidable for simple single-row writes — keep transactions scoped to genuinely multi-statement atomic operations (see `BatchOperationService.ExecuteBatchWithTransactionAsync` for the pattern: `_db.Database.BeginTransactionAsync()`, not a transaction wrapping the whole request). No retry logic at the DB layer — handle retries at the service layer instead (Npgsql's `EnableRetryOnFailure()` connection-resiliency option is available but not currently configured; don't add it without checking it doesn't conflict with explicit transaction usage, since EF Core's execution-strategy retries and manual transactions interact in ways that need `IExecutionStrategy.ExecuteAsync` wrapping, not naive retry). For batch operations, filter to only records with genuine changes to avoid oversized queries.

## No N+1 queries — see `ef-core.md` for the full pattern

This is worth restating here since it's the most common mistake when debugging "why is this endpoint slow": a loop issuing one query per iteration, or a second query for an entity already loaded and tracked in the same `DbContext`. Check `WebhookDeliveryService.PublishEventAsync` (customer-api) and `AuthService.LoginAsync`/`UserService.Verify2FAAsync` (admin-api) for real examples of this mistake having been made and then fixed during the migration — useful reference points for what to look for when reviewing a new service's query patterns.

## Optional: MongoDB

If a service needs NoSQL for high-throughput document storage, set it up as a separate connection in that service. Never mix EF Core/Postgres and MongoDB in the same domain. Configure connection via environment variables only.

## Environment variables

```env
DATABASE_URL="postgresql://user:password@host:5432/dbname"
```

Never hardcode connection strings — always read through `AddDotNetMonoRepoTemplateDatabase(connectionString)`, called once per service in `Program.cs`.

## Seeding

Not yet implemented for any entity in this repo (the Node era's `common/database/prisma/seed.ts` was deleted along with the rest of `common/database`'s Node source). Once EF Core migrations exist, static reference data (roles, statuses) belongs in `AppDbContext.OnModelCreating` via `entity.HasData(...)`:

```csharp
modelBuilder.Entity<Role>().HasData(
    new Role { Id = "role-super-admin", Name = RoleName.SuperAdmin, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
    new Role { Id = "role-moderator", Name = RoleName.Moderator, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
);
```

`HasData` seeds are baked into a migration (fixed `Id` values required, no `Guid.NewGuid()` at seed time) — don't reach for an imperative seed script (a `Program.cs`-invoked one-off) unless the data is genuinely dynamic/environment-specific, in which case it lives as a small console utility, not inside a service's request path.
