---
name: jwt-security
description: Use when scaffolding or reviewing authentication routes (login, logout, refresh, MFA) or any endpoint that issues, validates, or revokes JWTs. Supplements api-builder with mandatory token lifecycle rules — blacklist, refresh token rotation, and the per-user minIat "logout everywhere" marker. Always apply alongside api-builder when the domain involves auth.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

You implement JWT-based authentication on `System.IdentityModel.Tokens.Jwt`. These rules are mandatory and are not negotiable — do not simplify or skip them for any reason. **`AdminApi.Services.TokenService`/`AuthGuardMiddleware` is the canonical, fullest-featured, already-implemented reference** (blacklist + refresh tracking + `minIat` logout-everywhere + MFA challenge tokens) — read those two files before writing auth code in any other service, and copy the pattern rather than re-deriving it. `CustomerApi.Services.TokenService` implements a **lighter subset** (blacklist + refresh revocation, no `minIat`, no MFA) — that's a faithful port of what the Node-era `customer-api` actually had, not an oversight to silently "fix" by copying the admin-api pattern over without being asked.

## Token architecture (as actually implemented — not an idealized template)

- Access token: **1-hour TTL** (`AccessTokenExpiry = TimeSpan.FromHours(1)`), signed HS256, returned in the JSON response body (`LoginResponseData.AuthToken`) — **not** a cookie.
- Refresh token: `RememberMe` ? 30-day : 1-day TTL, signed HS256 with a **separate secret** (`JwtRefreshSecret`, distinct from the access-token secret), also returned in the JSON response body (`LoginResponseData.RefreshToken`) — **not** a cookie either. This differs from the HttpOnly-cookie pattern that's the textbook "more secure" choice; it's what was actually ported from the Node original, and changing it (to cookies, or to any storage mechanism) is a real breaking API-contract change for every frontend client, not a drop-in hardening tweak — don't do it without being asked.
- Every access and refresh token carries a `jti` claim (`Guid.NewGuid().ToString()`), a `type` claim (`"access"` or `"refresh"`, checked on verify so an access token can't be replayed as a refresh token or vice versa), and a `scope` claim (`TokenScope.Customer` or `TokenScope.Admin` — `AuthGuardMiddleware` rejects a token whose scope doesn't match the service it's presented to, so a `customer-api`-issued token can't authenticate against `admin-api`).
- Refresh tokens are tracked in **Redis**, not a database table: `token:refresh:{userId}:{tokenId}`, value is a small JSON blob (`{ createdAt, rememberMe }`), TTL matches the refresh token's own lifetime. There is no `RefreshToken` EF Core entity/table — don't add one without a reason beyond "the old illustrative doc used to show a Prisma model here."

**Logout must invalidate every active session for that user, not just the one that called logout — but only `admin-api` actually does this today.** A per-`jti` blacklist alone only revokes the single access token passed to the logout call — any other still-live access token issued to that user (a second tab, a second device) keeps working until it naturally expires. `admin-api`'s `TokenService` closes that gap with a per-user "invalidated before" marker, checked on every request in addition to the jti blacklist:

- On logout (`InvalidateAllAccessTokensAsync`): write `token:minIat:{userId} = <current unix seconds>` to Redis, TTL'd to the access token's own max lifetime (3600s) — no need to keep it past the point every pre-logout token would have expired anyway.
- On every authenticated request (`AuthGuardMiddleware` → `IsSessionInvalidatedAsync`), after the jti-blacklist check: read that marker and reject if the token's `iat` (extracted from the validated `JwtSecurityToken.Payload`, not re-parsed) is **strictly less than** the marker (`<`, not `<=` — using `<=` will reject a brand-new token issued in the exact same second as the logout that triggered a re-login, a real self-lockout bug, not just a theoretical one).
- This is on top of, not instead of, the existing per-jti blacklist and refresh-token revocation — the jti blacklist still covers the exact token passed to logout immediately (no TTL-second race), the `minIat` marker covers every *other* token that request didn't know about.
- `customer-api`'s `TokenService.LogoutAsync` does **not** implement this — it only blacklists the specific access token passed in and revokes all of that user's refresh tokens (`InvalidateAllUserRefreshTokensAsync`). A still-live access token from another device stays valid until it naturally expires (up to 1 hour). If a task explicitly asks for full logout-everywhere on `customer-api`, port the `minIat` pattern from `admin-api`'s `TokenService`/`AuthGuardMiddleware` verbatim — don't invent a different mechanism.

---

## File generation — auth domain

Auth follows the same layer pattern as `api-builder` (DTO → validator → service → endpoint) with the additions below.

### 1. DTOs (`Dtos/AuthDtos.cs`)

```csharp
public sealed record LoginRequestDto
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required bool RememberMe { get; init; }
    public string? Ip { get; set; }
}

public sealed record LoginResponseData
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
}

public sealed record LoginResponseDto : ResponseDto
{
    public LoginResponseData Data { get; init; } = new();
}
```

### 2. Validator (`Validators/AuthValidators.cs`)

```csharp
public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
```

### 3. Service — `Services/TokenService.cs`

The token-issuing/verifying primitive. Constructor takes `<Service>Options` (for `JwtSecret`/`JwtRefreshSecret`) and `IConnectionMultiplexer` (Redis). Key methods, mirroring the real implementation:

```csharp
public TokenPair GenerateToken(User user, bool rememberMe, string roleName)
{
    var refreshTokenId = Guid.NewGuid().ToString();
    var accessTokenId = Guid.NewGuid().ToString();
    var permissions = Rbac.GetPermissionsForRole(roleName);

    var accessToken = CreateToken(user.Id, user.Username, roleName, permissions, TokenScope.Customer, accessTokenId, "access", _accessKey, AccessTokenExpiry);
    var refreshExpiry = rememberMe ? RefreshTokenLongExpiry : RefreshTokenShortExpiry;
    var refreshToken = CreateToken(user.Id, user.Username, roleName, permissions, TokenScope.Customer, refreshTokenId, "refresh", _refreshKey, refreshExpiry);

    _ = StoreRefreshTokenAsync(user.Id, refreshTokenId, rememberMe); // fire-and-forget is intentional here — see note below

    return new TokenPair(accessToken, refreshToken, refreshTokenId);
}

public TokenPayload? VerifyAccessToken(string token) => Verify(token, _accessKey, "access");
public TokenPayload? VerifyRefreshToken(string token) => Verify(token, _refreshKey, "refresh");
```

The `_ = StoreRefreshTokenAsync(...)` fire-and-forget in `GenerateToken` is deliberate, not an oversight — `GenerateToken` itself is synchronous (matching the Node original's signature), and the Redis write is best-effort bookkeeping that shouldn't block token issuance. Don't "fix" it into an `await` without checking whether that changes the method's sync/async signature contract callers depend on.

### 4. Service — `Services/AuthService.cs`

```csharp
public async Task<LoginResponseDto> LoginAsync(LoginRequestDto model, CancellationToken cancellationToken = default)
{
    var lockKey = $"login:fail:{model.Email}";
    // ... Redis-counter lockout check (5 attempts, 15-minute TTL) — see "Login lockout" below

    var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == model.Email, cancellationToken);
    var passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user?.Password ?? DummyHash);
    if (user is null || !passwordValid)
    {
        // increment lockout counter, return generic "Invalid username or password"
    }

    var tokens = _tokenService.GenerateToken(user, model.RememberMe, user.Roles!.Name);
    // update status to Online, send login notification, return tokens
}
```

**Constant-time dummy compare**: `LoginAsync` always calls `BCrypt.Net.BCrypt.Verify` against *some* hash even when no user was found (`DummyHash`, a fixed bcrypt-shaped constant), so a timing attack can't distinguish "no such user" from "wrong password." Never short-circuit with `if (user is null) return early` before the compare — that reintroduces the timing side-channel this exists to close.

### 5. Endpoints (`Endpoints/AuthEndpoints.cs`)

```csharp
group.MapPost("/login", async (LoginRequestDto body, IValidator<LoginRequestDto> validator, AuthService authService) =>
{
    var validation = await validator.ValidateAsync(body);
    if (!validation.IsValid) { return validation.ToBadRequest(); }
    var result = await authService.LoginAsync(body);
    return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
}).AllowAnonymous().RequireRateLimiting("auth");
```

`login`/`refresh`/`forgot-password`/`reset-password` are `.AllowAnonymous().RequireRateLimiting("auth")`. `logout`/`me` require auth (no `.AllowAnonymous()`, picked up by `AuthGuardMiddleware` by default).

### 6. Auth middleware — `Auth/AuthGuardMiddleware.cs`

The full check order, in the actual implementation — do not reorder or skip a step:

1. `AllowAnonymous()` metadata present, or non-production `/docs` request → pass through
2. Extract bearer token from `Authorization` header → 401 if missing
3. `tokenService.VerifyAccessToken(token)` → 401 if invalid, or if `payload.Scope` doesn't match this service's expected scope
4. `payload.Jti` blacklist check (`IsTokenBlacklistedAsync`) → 401 if blacklisted
5. **`admin-api` only**: `IsSessionInvalidatedAsync(payload.Id, payload.Iat)` (the `minIat` check) → 401 if the token predates the user's last full logout
6. User lookup (`GetAuthorizedUserByIdAsync`, filtered to the service's expected role tier) → 401 if not found
7. `RequirePermissionsAttribute` metadata check, if present on the matched endpoint → 403 if the user's permissions don't cover all required ones
8. Populate `context.Items["CurrentUser"]`, call `_next(context)`

---

## Login lockout

Redis-counter lockout, independent of any framework rate limiter (the `"auth"` rate-limit tier throttles by IP; this throttles by account):

```csharp
var lockKey = $"login:fail:{model.Email}";
var lockCount = await db.StringGetAsync(lockKey);
if (lockCount.HasValue && int.Parse(lockCount!) >= LoginMaxAttempts) // 5
{
    return new LoginResponseDto { IsSuccessful = false, Message = "Account temporarily locked due to too many failed attempts. Try again later." };
}
// on failure:
var count = await db.StringIncrementAsync(lockKey);
if (count == 1) { await db.KeyExpireAsync(lockKey, TimeSpan.FromSeconds(LoginLockoutTtlSeconds)); } // 900s
// on success:
await db.KeyDeleteAsync(lockKey);
```

---

## Admin bootstrap — does not exist, do not invent it

`admin-api`'s non-negotiable rules describe a one-time `/auth/bootstrap-admin` route for creating the first `SUPER_ADMIN` account. **This route does not exist** in either the original Node code or the .NET port — confirmed by search before the migration touched auth. If a task asks you to add it, treat it as brand-new, security-sensitive functionality requiring the same scrutiny as any other auth-surface change (a design review, not a quick scaffold), never as "restoring" something that was supposedly already there. If you do build it, the two-lock shape below is the right design (a persisted `SystemBootstrap` singleton row as the real gate, plus an `AdminBootstrapEnabled` env kill-switch as defense in depth) — but don't write any of it speculatively as part of unrelated work.

---

## MFA / Two-Factor Authentication (TOTP)

Two distinct flows, both already implemented in `admin-api` — copy this pattern into any other service that needs MFA rather than re-deriving it.

### 1. Enrollment (already-authenticated user manages their own MFA)

`User.TwoFactorEnabled` (`bool`) and `User.TwoFactorSecret` (`string?`, **AES-256-GCM-encrypted at rest** via `TwoFactorEncryptionKey`, using `System.Security.Cryptography.AesGcm` — stored as `iv:authTag:ciphertext` hex-joined) already exist on the `User` entity. `AdminApi.Services.UserService` implements the full enrollment lifecycle using **Otp.NET** + **QRCoder**:

- `Setup2FAAsync(userId)` — generates a random secret (`KeyGeneration.GenerateRandomKey(20)`, base32-encoded via `Base32Encoding.ToString`), builds an `otpauth://` URI, renders it as a QR code (`QRCodeGenerator` → `PngByteQRCode` → base64 data URL), and stores the **encrypted** secret. Returns the raw secret (for manual entry) and the QR code data URL — never returns the encrypted form.
- `Verify2FAAsync(userId, token)` — verifies the first TOTP code against the pending secret and only then flips `TwoFactorEnabled = true`. Enrollment isn't "on" until a real code from the authenticator app proves the secret was actually scanned correctly.
- `Disable2FAAsync(userId, token)` — requires a valid current TOTP code (not just the session/password) to turn MFA off, clears both `TwoFactorEnabled` and `TwoFactorSecret`.
- `VerifyTotpCodeAsync(userId, code)` / the private `VerifyTotpCode(User, code)` overload — the shared verification primitive both of the above call, and the one the post-login check (below) calls too. Uses `Otp.NET`'s `new Totp(secretBytes).VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay)`, which returns a genuine `bool` — unlike some TOTP libraries that resolve a result *object* (always truthy regardless of validity), so there's no "forgot to destructure `.valid`" trap here the way there was in the Node era's `otplib`. Still: if you ever see TOTP verification anywhere in this codebase checking a non-`bool` return value as if it were one, that's a real authentication bypass, not a cosmetic issue — fix it immediately.
- **No redundant re-fetch**: `Verify2FAAsync`/`Disable2FAAsync` each already have the `User` entity loaded before calling verification — they call the private `VerifyTotpCode(User, code)` overload, not the public `VerifyTotpCodeAsync(userId, code)` (which re-queries). Don't reintroduce the double-query by switching back to the public overload inside a method that already has the entity in hand.

### 2. Post-login check (the part that's easy to forget to wire up)

Having enrollment alone does nothing if `LoginAsync` never looks at `TwoFactorEnabled`. The login flow is two steps when MFA is on:

```
POST /api/v1/auth/login              { email, password, rememberMe }
  → password wrong/user not admin-tier  → 401, same "Invalid username or password" as always
  → TwoFactorEnabled == false            → 200, real { authToken, refreshToken } — unchanged, single-step login
  → TwoFactorEnabled == true             → 200, { mfaRequired: true, mfaToken } — NO real tokens yet

POST /api/v1/auth/verify-login-mfa   { mfaToken, code, rememberMe }
  → mfaToken invalid/expired   → 401
  → code wrong                 → 401, rate-limited (see below)
  → code valid                 → 200, real { authToken, refreshToken } — login is now actually complete
```

`mfaToken` is a **separate, narrowly-scoped JWT** — `type: "mfa_challenge"`, 5-minute TTL, carries only `{ id: userId }`, signed with the same access-token key. It is not a bearer credential — `AuthGuardMiddleware` only accepts `type: "access"` tokens, so an `mfaToken` cannot be used to call any authenticated route, only `/auth/verify-login-mfa`. `TokenService.GenerateMfaChallengeToken`/`VerifyMfaChallengeToken` implement this.

Rate-limit the code-verification step independently from password login — a 6-digit TOTP code is only ~1,000,000 combinations, so without a lockout it's brute-forceable well within its 30-second validity window at any reasonable request rate. `AuthService.VerifyLoginMfaAsync` uses the same Redis-counter lockout pattern as password login (`mfa:fail:{userId}`, 5 attempts, 5-minute lockout) — copy that pattern, don't skip it because "TOTP is already secure."

Real tokens (`GenerateToken`), the "online" status update, and the login-notification email only fire once — either at the end of single-step `LoginAsync` when MFA is off, or at the end of `VerifyLoginMfaAsync` when MFA is on. Never fire them at the password-check step for an MFA-enabled user — password-correct is not the same as login-complete when a second factor is required.

### UI side — see `rules/frontend.md`'s "MFA enrollment and login challenge" section for the enrollment page and the post-login challenge screen pattern.

---

## Critical rules

Never skip the blacklist check in `AuthGuardMiddleware` — it runs on every authenticated request.
Never issue a new token pair on refresh-token reuse of an already-blacklisted/revoked token — `RefreshTokenAsync` already checks `IsTokenBlacklistedAsync` and `IsRefreshTokenValidAsync` before rotating; if either fails, return `null` (the endpoint maps that to 401), don't fall through to issuing anyway.
Never set a fixed TTL on a Redis blacklist/`minIat` key without deriving it from the token's actual remaining lifetime or the service's `AccessTokenTtlSeconds` constant.
Never omit `jti` from a signed access or refresh token — no `jti` means the token cannot be individually blacklisted.
Never store an access or refresh token in browser `localStorage`/`sessionStorage` on the frontend side of this contract — that's a frontend concern (`rules/frontend.md`), but it's the other half of what makes body-returned (non-cookie) tokens an acceptable tradeoff here: they must be held in memory only, not persisted client-side.
No `dynamic`. No unjustified `object`. No Data Annotations, no Zod on the backend. No comments in code. Always constructor-injected dependencies on `sealed class` services.
