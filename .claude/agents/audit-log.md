---
name: audit-log
description: Use when implementing audit trails for state-changing operations — adding audit logging to a service, deciding what to audit, querying audit history, or setting up retention policy. Trigger on "audit log", "track who changed this", or "log before/after values".
tools: Read, Edit, Write, Grep, Glob
model: inherit
---

Nothing under this pattern has been built yet anywhere in this codebase (Node or .NET) — this agent describes the target design, not something to extend from an existing implementation. Treat the first `AuditLogService` as new scaffolding, following the conventions below.

## When to audit

Not every table needs auditing. Audit state changes where knowing who changed what and when has regulatory, legal, or operational value: user accounts (role changes, deactivations), payment and order records, permissions and role assignments, settings/configuration, and anything explicitly marked auditable in requirements. Never audit read operations or log-noise entities (`WebhookDelivery`, queue jobs).

## Entity (`common/DotNetMonoRepoTemplate.Database/Entities/AuditLog.cs`)

```csharp
public sealed class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Entity { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public JsonDocument? Before { get; set; }
    public JsonDocument? After { get; set; }
    public required string ChangedBy { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

```csharp
modelBuilder.Entity<AuditLog>(entity =>
{
    entity.ToTable("audit_logs");
    entity.HasIndex(a => new { a.Entity, a.EntityId });
    entity.HasIndex(a => a.ChangedBy);
    entity.HasIndex(a => a.CreatedAt);
    entity.Property(a => a.Before).HasColumnType("jsonb");
    entity.Property(a => a.After).HasColumnType("jsonb");
});
```

`AuditLog` deliberately does **not** inherit `AuditableEntity`/`TimestampedEntity` — no `UpdatedAt`, no `IsActive`. Audit logs are immutable, append-only; there is no "who modified this audit entry" because nothing ever does.

## `AuditAction` constants (`common/DotNetMonoRepoTemplate.Types/AuditAction.cs`)

```csharp
public static class AuditAction
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Restore = "restore";
    public const string RoleAssign = "role_assign";
    public const string PasswordChange = "password_change";
    public const string Login = "login";
    public const string Logout = "logout";
}

public sealed record AuditContext
{
    public required string UserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
```

Static `const string` class, matching the ported string-literal-union convention used everywhere else in `DotNetMonoRepoTemplate.Types` — not a native C# `enum` (see `csharp-standards.md` for why).

## `AuditLogService`

```csharp
public sealed class AuditLogService
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "hash", "twoFactorSecret",
    };

    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db) => _db = db;

    public async Task LogAsync(
        string entity,
        string entityId,
        string action,
        IReadOnlyDictionary<string, object?>? before,
        IReadOnlyDictionary<string, object?>? after,
        AuditContext context,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Entity = entity,
            EntityId = entityId,
            Action = action,
            Before = before is null ? null : JsonDocument.Parse(JsonSerializer.Serialize(before)),
            After = after is null ? null : JsonDocument.Parse(JsonSerializer.Serialize(Redact(after))),
            ChangedBy = context.UserId,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent?.Length > 500 ? context.UserAgent[..500] : context.UserAgent,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, object?> Redact(IReadOnlyDictionary<string, object?> data) =>
        data.ToDictionary(kv => kv.Key, kv => SensitiveKeys.Contains(kv.Key) ? (object?)"[REDACTED]" : kv.Value);
}
```

## Using it in a domain service

Call `LogAsync` AFTER the DB write succeeds — see "Rules" below for why this can't be inside the same transaction as the entity write.

```csharp
public async Task<UserResponseDto> UpdateAsync(string id, UpdateUserDto dto, string userId, AuditContext context, CancellationToken cancellationToken = default)
{
    var existing = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
    if (existing is null)
    {
        return new UserResponseDto { IsSuccessful = false, Message = "User not found" };
    }

    var beforeSnapshot = new Dictionary<string, object?> { ["email"] = existing.Email, ["roleId"] = existing.RoleId };
    existing.Email = dto.Email;
    existing.RoleId = dto.RoleId;
    existing.ModifiedBy = userId;
    await _db.SaveChangesAsync(cancellationToken);

    await _auditLog.LogAsync(
        "user", id, AuditAction.Update, beforeSnapshot,
        new Dictionary<string, object?> { ["email"] = existing.Email, ["roleId"] = existing.RoleId },
        context, cancellationToken);

    return new UserResponseDto { IsSuccessful = true, Data = Map(existing) };
}
```

## Building `AuditContext` from the endpoint

```csharp
group.MapPut("/{id}", async (string id, UpdateUserDto body, UserService service, HttpContext context) =>
{
    var currentUser = context.GetCurrentUser();
    var auditContext = new AuditContext
    {
        UserId = currentUser?.Id ?? "SYSTEM",
        IpAddress = context.Connection.RemoteIpAddress?.ToString(),
        UserAgent = context.Request.Headers.UserAgent.ToString(),
    };
    var result = await service.UpdateAsync(id, body, currentUser?.Id ?? "SYSTEM", auditContext);
    return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
});
```

## What goes in before/after

Only the fields that could actually change — not the entire entity. Keeps payloads small and diffs readable. `before` is `null` for `Create` actions, `after` is `null` for `Delete` actions.

## Querying audit history

Add a read-only endpoint in `admin-api`: `GET /api/v1/audit-logs?entity=user&entityId={id}&page=1&pageSize=50`, gated with `.WithMetadata(new RequirePermissionsAttribute(PermissionName.ReportView))` or a dedicated `AuditRead` permission if the domain justifies one (see `rbac.md` for adding a new permission).

## Retention

`audit_logs` grows indefinitely. Add a scheduled cleanup job in `schedule-api` (via `CronSchedulerHostedService` — see the existing `WebhookProcessorJob` for the pattern) deleting entries older than the retention policy (e.g. 2 years for regulated industries, 90 days standard). Never manually delete audit entries outside the scheduled job.

## Rules

Never log before the DB write — if the write fails, the audit entry shouldn't exist. Never audit read operations. Always redact sensitive fields (`password`, `token`, `secret`) before writing `After`. Never write audit entries inside the same `AppDbContext.Database.BeginTransactionAsync()` scope as the entity write — a rollback would delete the audit too; keep them as separate `SaveChangesAsync()` calls (or, if genuine atomicity is required, a deliberate design decision to include both — but the default is separate). `Before`/`After` store only the changed-field subset, never the full entity.
