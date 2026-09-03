---
name: feature-flags
description: Use when implementing feature flags — creating a new flag, evaluating flags in a service or frontend, setting up the DB-backed flag store, or integrating an external provider like Unleash or LaunchDarkly. Also use when a flag needs to be removed after a full rollout or cleaned up after a cancelled feature.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

Nothing under this pattern has been built yet anywhere in this codebase (Node or .NET) — this agent describes the target design, not something to extend from an existing implementation.

## Strategy — DB-backed by default

Postgres-backed feature flag store by default: per-environment values via `DOTNET_ENVIRONMENT`/`NODE_ENV` (services still read `NODE_ENV` for app-level environment semantics — see `env-config.instructions.md`), per-user percentage rollouts, role-based targeting, no vendor costs. For large-scale production (100+ flags, real-time targeting, A/B analytics), migrate to Unleash (self-hosted) or LaunchDarkly by swapping the `FeatureFlagService` implementation behind the same interface — calling code doesn't change.

## Step 1 — EF Core entity (`common/DotNetMonoRepoTemplate.Database/Entities/FeatureFlag.cs`)

```csharp
public sealed class FeatureFlag : AuditableEntity
{
    public required string Key { get; set; }
    public required string Description { get; set; }
    public bool IsEnabled { get; set; }
    public int RolloutPercent { get; set; } = 100;
    public List<string> AllowedRoles { get; set; } = new();
    public List<string> AllowedUserIds { get; set; } = new();
    public List<string> Environments { get; set; } = new() { "development", "staging", "production" };
}
```

```csharp
modelBuilder.Entity<FeatureFlag>(entity =>
{
    entity.ToTable("feature_flags");
    entity.HasIndex(f => f.Key).IsUnique();
    entity.HasIndex(f => new { f.Key, f.IsEnabled, f.IsActive });
});
```

`List<string>` properties map to Postgres `text[]` via Npgsql's native array support — no extra configuration needed.

## Step 2 — Flag constants (`common/DotNetMonoRepoTemplate.Types/FeatureFlagKey.cs`)

```csharp
public static class FeatureFlagKey
{
    public const string NewCheckoutFlow = "new-checkout-flow";
    public const string EnhancedSearch = "enhanced-search";
    public const string BulkExport = "bulk-export";
}
```

No magic strings in application code — always reference these constants.

## Step 3 — `FeatureFlagService` (per-service, or a shared `DotNetMonoRepoTemplate.FeatureFlags` library if 3+ services need it)

```csharp
public sealed record FlagEvaluationContext
{
    public string? UserId { get; init; }
    public string? Role { get; init; }
}

public sealed class FeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private readonly string _environment;

    public FeatureFlagService(AppDbContext db, RedisCacheService cache, IHostEnvironment hostEnvironment)
    {
        _db = db;
        _cache = cache;
        _environment = hostEnvironment.EnvironmentName;
    }

    public async Task<bool> IsEnabledAsync(string flagKey, FlagEvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        var record = await _cache.GetAsync<FeatureFlagSnapshot>($"feature-flag:{flagKey}")
            ?? await FetchAndCacheAsync(flagKey, cancellationToken);
        if (record is null || !record.IsEnabled || !record.Environments.Contains(_environment))
        {
            return false;
        }
        if (context?.UserId is not null && record.AllowedUserIds.Contains(context.UserId)) { return true; }
        if (context?.Role is not null && record.AllowedRoles.Contains(context.Role)) { return true; }
        if (record.RolloutPercent < 100 && context?.UserId is not null)
        {
            return IsInRollout(context.UserId, flagKey, record.RolloutPercent);
        }
        return record.RolloutPercent == 100 || (context?.UserId is null && context?.Role is null);
    }

    private static bool IsInRollout(string userId, string flagKey, int percent)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{flagKey}:{userId}"));
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;
        return bucket < percent;
    }

    private async Task<FeatureFlagSnapshot?> FetchAndCacheAsync(string flagKey, CancellationToken cancellationToken)
    {
        var record = await _db.FeatureFlags
            .Where(f => f.Key == flagKey && f.IsActive)
            .Select(f => new FeatureFlagSnapshot(f.IsEnabled, f.RolloutPercent, f.AllowedRoles, f.AllowedUserIds, f.Environments))
            .FirstOrDefaultAsync(cancellationToken);
        if (record is not null)
        {
            await _cache.SetAsync($"feature-flag:{flagKey}", record, CacheTtl);
        }
        return record;
    }
}

public sealed record FeatureFlagSnapshot(bool IsEnabled, int RolloutPercent, List<string> AllowedRoles, List<string> AllowedUserIds, List<string> Environments);
```

The hash-based rollout is deterministic — the same user always lands in the same bucket, giving them a consistent experience across requests and sessions. `MD5` here is a bucketing hash, not a security primitive — using it for rollout-bucket assignment is fine; never use it anywhere a cryptographic guarantee is actually needed (that's `AesGcm`/`HMACSHA256`, already used correctly elsewhere — see `jwt-security.md`).

## Step 4 — Register in DI

```csharp
builder.Services.AddScoped<FeatureFlagService>();
```

## Step 5 — Using flags in services

```csharp
public async Task<OrderResponseDto> CheckoutAsync(CheckoutDto dto, string userId, string role, CancellationToken cancellationToken = default)
{
    var useNewFlow = await _featureFlags.IsEnabledAsync(FeatureFlagKey.NewCheckoutFlow, new FlagEvaluationContext { UserId = userId, Role = role }, cancellationToken);
    return useNewFlow ? await NewCheckoutFlowAsync(dto, userId, cancellationToken) : await LegacyCheckoutFlowAsync(dto, userId, cancellationToken);
}
```

## Step 6 — Exposing flags to the frontend

Public, aggressively cached endpoint in `customer-api`:

```csharp
group.MapGet("/", async (FeatureFlagService featureFlags) =>
    Results.Ok(new { isSuccessful = true, data = await featureFlags.GetAllAsync() })).AllowAnonymous();
```

Frontend `useFeatureFlags`/`useFlag` hooks are unchanged from the Node era — still React Query against this endpoint, no frontend-side change needed once the backend endpoint exists (see `rules/frontend.md` for the query-hook convention).

## Step 7 — Managing flags via admin API

`GET /api/v1/feature-flags` (`PermissionName.SettingsRead`), `PUT /api/v1/feature-flags/{key}` (`PermissionName.SettingsWrite`) — invalidate the flag's Redis cache key immediately when updated via the admin endpoint.

## Removing a flag after full rollout

1. Search the codebase for all `IsEnabledAsync(FeatureFlagKey.SomeFlag` usages
2. Remove the conditional, keep the new code path, delete the old one
3. Remove the constant from `FeatureFlagKey`
4. Hand off a migration to delete the row from `feature_flags` to the developer (never run `dotnet ef` yourself)

Never leave dead flags in the codebase — they accumulate into unreadable conditional forests.

## Critical rules

Never hardcode flag state (`if (true)`) — always check `FeatureFlagService`. Never evaluate the same flag multiple times per request — call once, store the result locally. Never use flags for security gates — flags can be bypassed, use RBAC (`RequirePermissionsAttribute`) for access control. Always define flag keys as `FeatureFlagKey` constants, never raw strings. Always seed new flags with `IsEnabled = false` and `RolloutPercent = 0` — opt-in, not opt-out.
