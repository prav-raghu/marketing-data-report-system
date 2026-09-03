---
applyTo: "apps/backend/*/src/**/*.cs,common/DotNetMonoRepoTemplate.*/**/*.cs"
---

# JWT Security — Token Revocation & Hijack Mitigation

> For generating the actual auth endpoint/service code (login, logout, refresh, MFA) that implements these rules, use the `jwt-security` subagent (`.claude/agents/jwt-security.md`) alongside `api-builder`. This file is the reference for the *why* and the mandatory rules; the agent is the *how*, with the real implemented code as its reference.

## Overview

JWTs are stateless by design. A client-side logout (clearing memory-held tokens) does
**not** invalidate a stolen token. This instruction defines the mandatory pattern for token
lifecycle management across all services in this monorepo.

---

## Token Architecture (as actually implemented)

| Token | TTL | Storage | Stateful |
|---|---|---|---|
| Access token | 1 hour | Returned in the JSON response body; frontend holds it in memory only (not `localStorage`) | No |
| Refresh token | 1–30 days (`RememberMe`-dependent) | Returned in the JSON response body; tracked in **Redis**, not a database table | Yes (Redis) |

- Access tokens are relatively short-lived to limit the blast radius of a stolen token.
- Refresh tokens are the revocable anchor — tracked in Redis (`token:refresh:{userId}:{tokenId}`) and rotated on every use.
- Never store access tokens in `localStorage`/`sessionStorage` on the frontend. Memory only.
- **Both tokens travel in the response body, not cookies** — this is a deliberate carry-over from the Node original's actual behavior, not the textbook "more secure" HttpOnly-cookie pattern. Changing this is a real breaking API-contract change, not a drop-in hardening tweak — don't do it without being asked (see `jwt-security.md`, the agent).

---

## Required Token Claims

Every signed access/refresh token **must** include a `jti` claim — a unique identifier per token issuance, used as the key for blacklist lookups — plus a `type` claim (`"access"`/`"refresh"`, checked on verify) and a `scope` claim (`TokenScope.Customer`/`TokenScope.Admin`, checked on verify so a token issued by one service can't authenticate against the other):

```csharp
var claims = new List<Claim>
{
    new("id", userId),
    new("username", username),
    new("role", role),
    new("scope", scope),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    new("type", type), // "access" or "refresh"
};
claims.AddRange(permissions.Select(permission => new Claim("permissions", permission)));
```

See `TokenService.CreateToken` (private, called by both `GenerateToken` and `GenerateMfaChallengeToken`'s sibling logic) for the real implementation.

---

## Blacklist — Redis (Required)

The blacklist is stored in Redis, **not** the primary database. Token verification is a
hot path — every authenticated request hits it. Redis gives O(1) lookup and automatic TTL
eviction so revoked entries are never stored longer than the token's natural lifespan.

### Add to blacklist on logout

```csharp
private async Task BlacklistTokenAsync(string tokenId, int ttlSeconds)
{
    var db = Database;
    if (db is null) { return; }
    await db.StringSetAsync($"{TokenBlacklistPrefix}{tokenId}", "1", TimeSpan.FromSeconds(ttlSeconds));
}
```

### Check blacklist in `AuthGuardMiddleware`

```csharp
if (payload.Jti is not null && await tokenService.IsTokenBlacklistedAsync(payload.Jti))
{
    await WriteUnauthorizedAsync(context);
    return;
}
```

The Redis key auto-expires when the token would have expired anyway — no manual cleanup required.

---

## Logout Handler

Logout must:
1. Add the access token's `jti` to the Redis blacklist.
2. Delete all refresh tokens for the user from Redis.
3. **`admin-api` only**: write the per-user `token:minIat:{userId}` marker so every other still-live access token for that user is also invalidated — see "Logout-Everywhere (`minIat`)" below. `customer-api`'s `LogoutAsync` does not do this (a faithful port of the Node original's narrower scope on that service).

```csharp
public async Task LogoutAsync(string userId, string? accessToken, string? refreshToken)
{
    var tasks = new List<Task> { InvalidateAllUserRefreshTokensAsync(userId) };
    // admin-api's TokenService additionally adds: InvalidateAllAccessTokensAsync(userId)

    if (accessToken is not null)
    {
        var accessPayload = VerifyAccessToken(accessToken);
        if (accessPayload?.Jti is not null)
        {
            tasks.Add(BlacklistTokenAsync(accessPayload.Jti, AccessTokenTtlSeconds));
        }
    }
    if (refreshToken is not null)
    {
        var refreshPayload = VerifyRefreshToken(refreshToken);
        if (refreshPayload?.Jti is not null)
        {
            tasks.Add(BlacklistTokenAsync(refreshPayload.Jti, RefreshBlacklistTtlSeconds));
            tasks.Add(RemoveRefreshTokenAsync(userId, refreshPayload.Jti));
        }
    }
    await Task.WhenAll(tasks);
}
```

---

## Refresh Token Rotation

A refresh token is **single-use**. On every `/api/v1/auth/refresh` call:

1. Verify the presented token's signature/type (`VerifyRefreshToken`).
2. Check it's not blacklisted, and that it's still present in Redis (`IsRefreshTokenValidAsync`) — if either check fails, return `null` (the endpoint maps that to 401). There is no separate "reuse detected → nuke all sessions" branch distinct from this — an invalid/already-rotated token simply fails verification and the caller gets 401; it doesn't trigger an automatic mass-revocation the way some illustrative JWT guides describe. If a task specifically needs reuse-triggers-mass-revocation semantics, that's new behavior to design deliberately, not something already implemented.
3. If valid → blacklist it, remove it from Redis, issue a new access+refresh pair.

```csharp
public async Task<TokenPair?> RefreshTokenAsync(string token, bool rememberMe)
{
    var payload = VerifyRefreshToken(token);
    if (payload?.Jti is null || string.IsNullOrEmpty(payload.Id)) { return null; }
    if (await IsTokenBlacklistedAsync(payload.Jti)) { return null; }
    if (!await IsRefreshTokenValidAsync(payload.Id, payload.Jti)) { return null; }

    await BlacklistTokenAsync(payload.Jti, GetRefreshTokenTtl(rememberMe));
    await RemoveRefreshTokenAsync(payload.Id, payload.Jti);

    return GenerateToken(/* the user, rememberMe, payload.Role */);
}
```

---

## Logout-Everywhere (`minIat`) — `admin-api` only

```csharp
public async Task InvalidateAllAccessTokensAsync(string userId)
{
    var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    await Database!.StringSetAsync($"{MinIatPrefix}{userId}", nowSeconds.ToString(), TimeSpan.FromSeconds(AccessTokenTtlSeconds));
}

public async Task<bool> IsSessionInvalidatedAsync(string userId, long? issuedAt)
{
    if (issuedAt is null) { return false; }
    var minIat = await Database!.StringGetAsync($"{MinIatPrefix}{userId}");
    if (!minIat.HasValue) { return false; }
    return long.TryParse(minIat, out var minIatValue) && issuedAt < minIatValue; // strictly less-than, not <=
}
```

Checked in `AuthGuardMiddleware` right after the blacklist check, before the user lookup — `admin-api` only. See `jwt-security.md` (the agent) for the full "why `<`, not `<=`" reasoning and why `customer-api` doesn't have this.

---

## Redis Key Reference

```
token:blacklist:{tokenId}         → "1", TTL = remaining token lifetime
token:refresh:{userId}:{tokenId}  → JSON { createdAt, rememberMe }, TTL = refresh token lifetime
token:minIat:{userId}             → unix-seconds timestamp, TTL = access token lifetime (admin-api only)
```

There is no `RefreshToken` EF Core entity/database table — all refresh-token state lives in Redis.

---

## Cookie Configuration — not applicable

Neither token is set as a cookie in this implementation — both travel in the JSON response body (see "Token Architecture" above). Do not add `HttpOnly`/`secure`/`sameSite` cookie-setting code to any auth endpoint without being explicitly asked to change the token-transport mechanism — that's a deliberate architectural decision already made, not an omission.

---

## Rules for Agents

- Every JWT signing call **must** include a `jti` claim (`Guid.NewGuid().ToString()`). No exceptions.
- The blacklist check in `AuthGuardMiddleware` is **not optional** — do not skip it for performance.
- Never store access or refresh tokens in `localStorage`/`sessionStorage` on the frontend — memory only, since both are returned in the body rather than an `HttpOnly` cookie that would otherwise partially mitigate this.
- Refresh token rotation is single-use. A blacklisted/invalid/already-rotated token presented again simply fails and returns 401 — see "Refresh Token Rotation" above for what is and isn't implemented here.
- The Redis blacklist TTL must match the remaining lifetime of the token, not a fixed value.
- Do not query a database table for token revocation state on the hot path. Redis only.
- The `minIat` logout-everywhere pattern is `admin-api`-only — don't assume `customer-api` has it, and don't silently add it there without being asked (see `jwt-security.md` for why that's a deliberate scope difference, not a bug).
