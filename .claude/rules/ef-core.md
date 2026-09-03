---
paths:
  - "common/DotNetMonoRepoTemplate.Database/**/*.cs"
---

# EF Core Entity Rules

You are working on the shared database library (`common/DotNetMonoRepoTemplate.Database`) — `AppDbContext` and the entities under `Entities/`. Every change here affects all four backend services.

This replaces the old Prisma-schema rules (`schema.prisma` no longer exists in this repo — see `documentation/dotnet-migration-plan.md` §6.1 for why). EF Core points at the **same live Postgres schema** Prisma created; no new migration should introduce schema drift without the developer's explicit go-ahead (see "Never" below).

## Every business entity must have all six base fields

Inherit `AuditableEntity`, not hand-roll the properties:

```csharp
public sealed class Order : AuditableEntity
{
    public required string CustomerId { get; set; }
    // ...
}
```

`AuditableEntity` provides:

```csharp
public abstract class AuditableEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; } = "SYSTEM";
    public string? ModifiedBy { get; set; } = "SYSTEM";
}
```

`AppDbContext.SaveChanges()`/`SaveChangesAsync()` already stamp `CreatedAt`/`UpdatedAt` automatically on every `AuditableEntity`/`TimestampedEntity` (see the `ApplyTimestamps()` override) — never set those two by hand. `CreatedBy`/`ModifiedBy` are **not** auto-stamped (the DbContext doesn't know who the acting user is) — the service layer must set them explicitly on create/update from the authenticated principal, or leave the `"SYSTEM"` default for genuinely non-interactive writes. A model with timestamps set but `CreatedBy`/`ModifiedBy` always left at `"SYSTEM"` on a user-facing write path is a bug, not a style choice.

### Two sanctioned exceptions — narrow, and only for these shapes

1. **Truly append-only, never updated** (a pure event/audit-trail row): inherit `TimestampedEntity` instead — drops `IsActive`, `CreatedBy`, `ModifiedBy`, keeps `Id`/`CreatedAt`/`UpdatedAt`. There is no "who modified this" because nothing ever does. Join tables and lookup tables fall in this category too. See `WebhookDelivery : TimestampedEntity`.

2. **System-owned, never human-actioned, but internally mutated** (e.g. a background worker updates retry/status fields, no user-facing endpoint ever writes to it): `TimestampedEntity` still fits — `UpdatedAt` changes (the row *does* mutate), but `CreatedBy`/`ModifiedBy` would only ever read `"SYSTEM"`, which carries no information. `WebhookDelivery` is exactly this: `WebhookProcessorJob`/`WebhookDeliveryService` update `Status`/`AttemptCount`/`NextRetryAt` on retries, but no human or per-request actor ever writes to it.

Anything reachable from a user-facing request — even indirectly, even a nullable/optional `CreatedBy` on a system-registered resource — keeps the full six via `AuditableEntity`. See `WebhookSubscription : AuditableEntity`: registered via an admin endpoint (so `CreatedBy` is populated, if only with `"SYSTEM"` for bootstrap-created subscriptions) and mutated via admin endpoints (`IsActive` toggle, retry/timeout config) — it keeps both `CreatedBy` and `ModifiedBy`. Do not drop `ModifiedBy` on an entity just because it originated as a system integration; drop it only when category 1 or 2 genuinely applies.

## Naming — handled automatically, don't fight it

`AddDotNetMonoRepoTemplateDatabase` configures `UseSnakeCaseNamingConvention()` (the `EFCore.NamingConventions` package) once, in `DatabaseServiceCollectionExtensions.cs`. Every PascalCase C# property maps to its `snake_case` column automatically — `UserStatusId` → `user_status_id`, `WebhookSubscription` → `webhook_subscriptions` (pluralized table names come from `entity.ToTable("webhook_subscriptions")` calls in `AppDbContext.OnModelCreating`, which still need to be explicit; the naming convention only handles the PascalCase→snake_case translation, not pluralization). Never add a manual `[Column("...")]` attribute to work around a naming mismatch — if the convention produces the wrong column name, the fix is in `OnModelCreating`, not a per-property override.

- Foreign key properties: `{RelatedEntity}Id` (C#) → `{related_table}_id` (DB), e.g. `RoleId` → `role_id`
- Entity class names: PascalCase singular (`WebhookDelivery`, not `WebhookDeliveries`)
- `entity.ToTable("...")`: snake_case plural, set explicitly per entity in `OnModelCreating`

## Field types

| Data | C# type | Notes |
|---|---|---|
| Primary key | `string` (GUID as string, matches `AuditableEntity.Id`/`TimestampedEntity.Id`) | Never `Guid` — the DB column and existing rows are `text`/`varchar`, not `uuid`, from the Prisma era |
| Money / currency | `decimal`, `entity.Property(x => x.Amount).HasColumnType("decimal(10,2)")` | |
| Short text | `string`, `.HasMaxLength(N)` matching the original `@db.VarChar(N)` | |
| Long text | `string`, no `HasMaxLength` | |
| Status / category | `static class` of `const string` values (see `DotNetMonoRepoTemplate.Types` — `WebhookDeliveryStatus`, `ReportStatus`, etc.), **not** a native C# `enum` | Ported TS string-literal unions map to string constants for exact wire-format fidelity — a native `enum` would serialize as an integer by default and break the JSON contract |
| Timestamps | `DateTime` (UTC) | |
| JSON payload | `System.Text.Json.JsonDocument`, `entity.Property(x => x.Payload).HasColumnType("jsonb")` | See `WebhookDelivery.Payload` |

## Relations

Always configure both sides in `OnModelCreating`. Use `DeleteBehavior.Cascade` for child records, `DeleteBehavior.Restrict` for referenced lookup entities (roles, statuses) where an accidental cascade would silently wipe unrelated data, `DeleteBehavior.SetNull` for optional references. Always add `entity.HasIndex(x => x.ForeignKeyProperty)` on every foreign key.

```csharp
modelBuilder.Entity<WebhookDelivery>(entity =>
{
    entity.ToTable("webhook_deliveries");
    entity.HasIndex(d => d.SubscriptionId);
    entity
        .HasOne(d => d.Subscription)
        .WithMany(s => s.Deliveries)
        .HasForeignKey(d => d.SubscriptionId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

## Every entity needs a cursor-pagination index

```csharp
entity.HasIndex(x => new { x.CreatedAt, x.Id }).IsDescending(true, false);
```

## Composite indexes for common query patterns

```csharp
entity.HasIndex(x => new { x.CategoryId, x.IsActive, x.CreatedAt });
entity.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
```

## Optimistic locking

For entities with concurrent write risk (orders, inventory, cart, payments), add a concurrency token:

```csharp
entity.Property(x => x.Version).IsConcurrencyToken();
```

Catch `DbUpdateConcurrencyException` in the service layer and translate it to a 409 response — never let it surface as an unhandled 500.

## Idempotency

For write-heavy transactional entities:

```csharp
public string? IdempotencyKey { get; set; }
```

```csharp
entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("idempotency_key IS NOT NULL");
```

## No N+1 queries — the most common EF Core mistake

- Loop over a collection and issue a query per iteration → load everything up front with `.Include()` (for navigation properties you'll traverse) or a projection `.Select()` (for a flat DTO shape), then loop over already-materialized data
- Never re-query for an entity you already have loaded and tracked in the same `DbContext` instance — if `AuthService.LoginAsync` already fetched `user` with `.Include(u => u.Roles)`, use `user.Roles`, don't separately call `_db.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId)` a few lines later
- Batch `SaveChangesAsync()` calls outside a loop when the loop body is pure data mutation with no independent per-item I/O; keep per-item `SaveChangesAsync()` only when each iteration does real external work (an HTTP call, e.g. `WebhookDeliveryService.DeliverWebhookAsync`) where losing durability on the whole batch because item 6 crashed is the worse outcome
- See `WebhookDeliveryService.PublishEventAsync` for the corrected pattern (was previously re-querying + saving per-subscription; now batches the create + a single `SaveChangesAsync()`)

## Migrations and seeding

EF Core migrations haven't been generated yet in this repo — the live schema is Prisma's original, and the plan (`dotnet-migration-plan.md` §7) is a deliberate no-new-DDL overlap period. When the developer is ready to start EF Core migrations as the schema's source of truth again:

```bash
dotnet ef migrations add InitialCreate --project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/customer-api/src
dotnet ef database update --project common/DotNetMonoRepoTemplate.Database --startup-project apps/backend/customer-api/src
```

The first migration against an already-existing schema should be a **baseline** (no actual DDL runs, or the migration is manually edited to a no-op) — never let `dotnet ef migrations add` generate real `CREATE TABLE` statements for tables that already exist.

Seed data (once migrations exist) goes in `AppDbContext.OnModelCreating` via `entity.HasData(...)` for genuinely static reference data (roles, statuses) — do not write imperative seed scripts unless the data is dynamic/environment-specific.

## Never

- Never run `dotnet ef database update` or `dotnet ef migrations add` yourself — the developer runs migrations, per the project's non-negotiable rules
- Never hardcode connection strings — `DATABASE_URL` only, read through `AddDotNetMonoRepoTemplateDatabase`
- Never skip `entity.ToTable("...")` on a model in `OnModelCreating`
- Never add a property without confirming its snake_case column name matches what Prisma already created (the live schema is the source of truth, not the C# property name) — if in doubt, check the deleted `schema.prisma`'s history in git, or query the live DB's `information_schema.columns`
