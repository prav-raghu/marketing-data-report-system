---
name: rbac
description: Use when implementing role-based access control — adding permissions to endpoints, defining role-to-permission mappings, working with RequirePermissionsAttribute, or restricting service methods to specific roles. Also use when a new role or permission needs to be added to the system, or when auditing which endpoints are accessible to which roles.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

## Overview

RBAC is built on three layers, all in `common/DotNetMonoRepoTemplate.Types`: permissions (fine-grained `entity:action` string constants), role-to-permission mapping (`Rbac.RolePermissions`), and a permission check inside `AuthGuardMiddleware` driven by a `[RequirePermissionsAttribute(...)]` on the endpoint.

## Creating the first SUPER_ADMIN

Never create the first admin account through the normal user-creation path with the guard disabled or bypassed. **There is currently no sanctioned way in** — the one-time, self-locking `/auth/bootstrap-admin` route described in older docs does not exist in this codebase (Node or .NET), confirmed by search. See `jwt-security.md`'s "Admin bootstrap" section before building one; it's new functionality requiring a design review, not something to restore.

## Support starts read-only

`Support` is deliberately scoped to read-only permissions at launch — no write, manage, delete, export, or assign (it currently gets `UserRead`, `RoleRead`, `ReportView`). This is the default for a new project, not a limitation to work around. Only add write-shaped permissions to `Rbac.RolePermissions[RoleName.Support]` when the actual product scope calls for it (e.g. "Support needs to log tickets" → add a `TicketWrite` permission once a ticketing domain exists). Elevate one permission at a time, deliberately, in the same PR that introduces the feature it's for — never grant `Support` broad write access preemptively "in case it's needed later."

## Existing roles (`common/DotNetMonoRepoTemplate.Types/RoleName.cs`)

| Role | Tier | Description |
|---|---|---|
| `SuperAdmin` | Admin | Full system access — always gets `PermissionName.All`, never a hand-maintained list |
| `Moderator` | Admin | Moderate content, manage users below their level |
| `Support` | Admin | Read-only by default — see "Support starts read-only" above |
| `ChatUser` | Customer | Customer-facing features only |

`RoleName.AdminTierRoles` = `[SuperAdmin, Moderator, Support]`, `RoleName.CustomerTierRoles` = `[ChatUser]` — used by `AuthService`/`UserService` to gate which roles can authenticate against `admin-api` vs. `customer-api`.

## Step 1 — Define permissions (`common/DotNetMonoRepoTemplate.Types/PermissionName.cs`)

Static class of `const string`, not a native C# `enum` — matches the ported TS string-literal-union convention used everywhere else in `DotNetMonoRepoTemplate.Types` (`RoleName`, `ReportType`, `WebhookDeliveryStatus`, etc.) for exact wire-format fidelity:

```csharp
public static class PermissionName
{
    public const string UserRead = "user:read";
    public const string UserWrite = "user:write";
    public const string UserDelete = "user:delete";
    public const string RoleRead = "role:read";
    public const string RoleAssign = "role:assign";
    public const string ReportView = "report:view";
    public const string ReportExport = "report:export";
    public const string SettingsRead = "settings:read";
    public const string SettingsWrite = "settings:write";
    public const string BatchWrite = "batch:write";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UserRead, UserWrite, UserDelete, RoleRead, RoleAssign,
        ReportView, ReportExport, SettingsRead, SettingsWrite, BatchWrite,
    };
}
```

## Step 2 — Role-permission map (`common/DotNetMonoRepoTemplate.Types/Rbac.cs`)

```csharp
public static class Rbac
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleName.SuperAdmin] = PermissionName.All,
            [RoleName.Moderator] = new[] { PermissionName.UserRead, PermissionName.UserWrite, PermissionName.RoleRead, PermissionName.ReportView, PermissionName.ReportExport, PermissionName.BatchWrite },
            [RoleName.Support] = new[] { PermissionName.UserRead, PermissionName.RoleRead, PermissionName.ReportView },
            [RoleName.ChatUser] = new[] { PermissionName.UserRead },
        };

    public static IReadOnlyList<string> GetPermissionsForRole(string role) =>
        RolePermissions.TryGetValue(role, out var permissions) ? permissions : Array.Empty<string>();

    public static bool RoleHasPermission(string role, string permission) =>
        RolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}
```

## Step 3 — Permissions are resolved into the JWT at sign time

`TokenService.GenerateToken`/`CreateToken` call `Rbac.GetPermissionsForRole(roleName)` and embed the result as repeated `"permissions"` claims — the guard checks without a DB lookup per request. See `jwt-security.md` for the full token-issuing flow; don't re-derive this, both `TokenService` implementations already do it.

## Step 4 — `RequirePermissionsAttribute` (`Auth/RequirePermissionsAttribute.cs`)

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequirePermissionsAttribute : Attribute
{
    public RequirePermissionsAttribute(params string[] permissions) => Permissions = permissions;
    public IReadOnlyList<string> Permissions { get; }
}
```

## Step 5 — The check lives inside `AuthGuardMiddleware`, not a separate hook

Unlike the Fastify era (a dedicated `permission.guard.ts` hook registered after the JWT hook), the .NET port folds the permission check into the same `AuthGuardMiddleware` that already does token verification and user lookup — one middleware, not two:

```csharp
var requiredPermissions = context.GetEndpoint()?.Metadata.GetMetadata<RequirePermissionsAttribute>()?.Permissions;
if (requiredPermissions is { Count: > 0 })
{
    var userPermissions = new HashSet<string>(currentUser.Permissions);
    if (!requiredPermissions.All(userPermissions.Contains))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { isSuccessful = false, message = "Forbidden: insufficient permissions" });
        return;
    }
}
```

Do not add a second middleware for this — `AuthGuardMiddleware` is the single place auth and permission checks happen, per `rules/backend.md`'s pipeline order.

## Step 6 — Protect endpoints

```csharp
group.MapPost("/", async (CreateProductDto body, ProductService service, HttpContext context) => { /* ... */ })
    .WithMetadata(new RequirePermissionsAttribute(PermissionName.SettingsWrite));

group.MapGet("/", async (ProductService service) => { /* ... */ })
    .AllowAnonymous(); // public list endpoint — no permission needed
```

`admin-api`'s `BatchEndpoints.cs` is the canonical real example — every batch route requires `PermissionName.BatchWrite` via `.WithMetadata(new RequirePermissionsAttribute(...))` on the `MapGroup`, the first place in the migration this mechanism is actually exercised end-to-end (`customer-api` has the attribute wired but no route currently uses it).

## Permission naming convention

`{entity}:{action}` — `read` (list + get, safe), `write` (create + update), `delete` (soft delete), `manage` (elevated write — approve/reject/escalate), `export` (download/bulk export), `assign` (assign to another entity, e.g. role to user).

## Adding a new permission

1. Add the constant to `PermissionName.cs`, and to `PermissionName.All`
2. Add it to the appropriate roles in `Rbac.RolePermissions`
3. Add it to the relevant endpoint's `.WithMetadata(new RequirePermissionsAttribute(...))`

## Adding a new role

1. Add to `RoleName.cs`, and to `RoleName.All`
2. Add to `AdminTierRoles` or `CustomerTierRoles` if applicable
3. Add an entry in `Rbac.RolePermissions`
4. Seed the `Role` row once EF Core migrations/seeding exist (see `ef-core.md`) — until then, the role row is created directly in Postgres or via the existing (Prisma-era) data, since no application code path currently creates roles

## Critical rules

Never check roles directly in service methods — only check permissions, and only via `AuthGuardMiddleware`. Never put permission logic inside an endpoint delegate — that belongs in the middleware, reading `RequirePermissionsAttribute` metadata. Public endpoints (`.AllowAnonymous()`) skip all auth — only for truly unauthenticated endpoints. `SuperAdmin` always gets `PermissionName.All` — never list its permissions manually. Permissions are embedded in the JWT at login time — changing `Rbac.RolePermissions` requires re-login (or, for `admin-api`, a forced session invalidation via the `minIat` mechanism — see `jwt-security.md`) to take effect for already-logged-in users.
