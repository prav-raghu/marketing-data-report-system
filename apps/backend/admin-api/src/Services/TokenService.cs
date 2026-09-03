using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AdminApi.Configuration;
using Microsoft.IdentityModel.Tokens;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Types;
using StackExchange.Redis;

namespace AdminApi.Services;

public sealed class TokenService
{
    private const string TokenAlgorithm = SecurityAlgorithms.HmacSha256;
    private const string TokenBlacklistPrefix = "token:blacklist:";
    private const string RefreshTokenPrefix = "token:refresh:";
    private const string MinIatPrefix = "token:minIat:";

    private static readonly TimeSpan AccessTokenExpiry = TimeSpan.FromHours(1);
    private const int AccessTokenTtlSeconds = 3600;
    private static readonly TimeSpan MfaChallengeExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshTokenShortExpiry = TimeSpan.FromDays(1);
    private static readonly TimeSpan RefreshTokenLongExpiry = TimeSpan.FromDays(30);
    private const int ShortSessionTtlSeconds = 24 * 3600;
    private const int LongSessionTtlSeconds = 30 * 24 * 3600;
    private const int RefreshBlacklistTtlSeconds = 30 * 24 * 3600;

    private readonly SymmetricSecurityKey _accessKey;
    private readonly SymmetricSecurityKey _refreshKey;
    private readonly IConnectionMultiplexer _redis;
    private readonly Logger _logger = new(nameof(TokenService));

    public TokenService(AdminApiOptions options, IConnectionMultiplexer redis)
    {
        _accessKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret));
        _refreshKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtRefreshSecret));
        _redis = redis;
    }

    private IDatabase? Database => _redis.IsConnected ? _redis.GetDatabase() : null;

    public TokenPair GenerateToken(User user, bool rememberMe, string roleName)
    {
        var refreshTokenId = Guid.NewGuid().ToString();
        var accessTokenId = Guid.NewGuid().ToString();
        var permissions = Rbac.GetPermissionsForRole(roleName);

        var accessToken = CreateToken(
            user.Id, user.Username, roleName, permissions, TokenScope.Admin, accessTokenId, "access", _accessKey, AccessTokenExpiry);

        var refreshExpiry = rememberMe ? RefreshTokenLongExpiry : RefreshTokenShortExpiry;
        var refreshToken = CreateToken(
            user.Id, user.Username, roleName, permissions, TokenScope.Admin, refreshTokenId, "refresh", _refreshKey, refreshExpiry);

        _ = StoreRefreshTokenAsync(user.Id, refreshTokenId, rememberMe);

        return new TokenPair(accessToken, refreshToken, refreshTokenId);
    }

    public string GenerateMfaChallengeToken(string userId)
    {
        var claims = new List<Claim> { new("id", userId), new("type", "mfa_challenge") };
        var credentials = new SigningCredentials(_accessKey, TokenAlgorithm);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.Add(MfaChallengeExpiry), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public MfaChallengePayload? VerifyMfaChallengeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = _accessKey,
                    ValidAlgorithms = new[] { TokenAlgorithm },
                },
                out _);

            var type = principal.FindFirstValue("type");
            if (type != "mfa_challenge")
            {
                _logger.Warn("Token type mismatch - expected mfa_challenge token");
                return null;
            }

            return new MfaChallengePayload { Id = principal.FindFirstValue("id") ?? string.Empty, Type = type };
        }
        catch (Exception ex)
        {
            _logger.Debug("MFA challenge token verification failed", new Dictionary<string, object?> { ["error"] = ex.Message });
            return null;
        }
    }

    private static string CreateToken(
        string userId,
        string username,
        string role,
        IReadOnlyList<string> permissions,
        string scope,
        string jti,
        string type,
        SymmetricSecurityKey key,
        TimeSpan expiry)
    {
        var claims = new List<Claim>
        {
            new("id", userId),
            new("username", username),
            new("role", role),
            new("scope", scope),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("type", type),
        };
        claims.AddRange(permissions.Select(permission => new Claim("permissions", permission)));

        var credentials = new SigningCredentials(key, TokenAlgorithm);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.Add(expiry), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenPayload? VerifyAccessToken(string token) => Verify(token, _accessKey, "access");

    public TokenPayload? VerifyRefreshToken(string token) => Verify(token, _refreshKey, "refresh");

    private TokenPayload? Verify(string token, SymmetricSecurityKey key, string expectedType)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = key,
                    ValidAlgorithms = new[] { TokenAlgorithm },
                },
                out var validatedToken);

            var type = principal.FindFirstValue("type");
            if (type != expectedType)
            {
                _logger.Warn("Token type mismatch");
                return null;
            }

            var jwt = (JwtSecurityToken)validatedToken;
            var iat = jwt.Payload.TryGetValue("iat", out var iatValue) && iatValue is not null
                ? Convert.ToInt64(iatValue)
                : (long?)null;

            return new TokenPayload
            {
                Id = principal.FindFirstValue("id") ?? string.Empty,
                Username = principal.FindFirstValue("username") ?? string.Empty,
                Role = principal.FindFirstValue("role") ?? string.Empty,
                Permissions = principal.FindAll("permissions").Select(claim => claim.Value).ToList(),
                Scope = principal.FindFirstValue("scope") ?? string.Empty,
                Jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti),
                Type = type,
                Iat = iat,
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("Token verification failed", new Dictionary<string, object?> { ["error"] = ex.Message });
            return null;
        }
    }

    public async Task<TokenPair?> RefreshTokenAsync(string token, bool rememberMe)
    {
        var payload = VerifyRefreshToken(token);
        if (payload?.Jti is null || string.IsNullOrEmpty(payload.Id))
        {
            return null;
        }

        if (await IsTokenBlacklistedAsync(payload.Jti))
        {
            _logger.Warn("Attempted use of blacklisted refresh token", new Dictionary<string, object?> { ["userId"] = payload.Id });
            return null;
        }

        if (!await IsRefreshTokenValidAsync(payload.Id, payload.Jti))
        {
            _logger.Warn("Refresh token not found or invalidated", new Dictionary<string, object?> { ["userId"] = payload.Id });
            return null;
        }

        await BlacklistTokenAsync(payload.Jti, GetRefreshTokenTtl(rememberMe));
        await RemoveRefreshTokenAsync(payload.Id, payload.Jti);

        var newTokens = GenerateToken(
            new User { Id = payload.Id, Username = payload.Username, Password = string.Empty, Email = string.Empty, IpAddress = string.Empty, RoleId = string.Empty, UserStatusId = string.Empty },
            rememberMe,
            payload.Role);

        _logger.Info("Refresh token rotated successfully", new Dictionary<string, object?> { ["userId"] = payload.Id });
        return newTokens;
    }

    public async Task LogoutAsync(string userId, string? accessToken, string? refreshToken)
    {
        var tasks = new List<Task>
        {
            InvalidateAllUserRefreshTokensAsync(userId),
            InvalidateAllAccessTokensAsync(userId),
        };

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
        _logger.Info("User logged out successfully", new Dictionary<string, object?> { ["userId"] = userId });
    }

    public async Task<bool> IsTokenBlacklistedAsync(string tokenId)
    {
        var db = Database;
        if (db is null)
        {
            return false;
        }
        try
        {
            return await db.KeyExistsAsync($"{TokenBlacklistPrefix}{tokenId}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to check token blacklist", ex);
            return true;
        }
    }

    public async Task InvalidateAllAccessTokensAsync(string userId)
    {
        var db = Database;
        if (db is null)
        {
            return;
        }
        try
        {
            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.StringSetAsync($"{MinIatPrefix}{userId}", nowSeconds.ToString(), TimeSpan.FromSeconds(AccessTokenTtlSeconds));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to invalidate access tokens", ex);
        }
    }

    public async Task<bool> IsSessionInvalidatedAsync(string userId, long? issuedAt)
    {
        var db = Database;
        if (db is null || issuedAt is null)
        {
            return false;
        }
        try
        {
            var minIat = await db.StringGetAsync($"{MinIatPrefix}{userId}");
            if (!minIat.HasValue)
            {
                return false;
            }
            return long.TryParse(minIat, out var minIatValue) && issuedAt < minIatValue;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to check session invalidation", ex);
            return true;
        }
    }

    private async Task BlacklistTokenAsync(string tokenId, int ttlSeconds)
    {
        var db = Database;
        if (db is null)
        {
            return;
        }
        try
        {
            await db.StringSetAsync($"{TokenBlacklistPrefix}{tokenId}", "1", TimeSpan.FromSeconds(ttlSeconds));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to blacklist token", ex);
        }
    }

    private async Task StoreRefreshTokenAsync(string userId, string tokenId, bool rememberMe)
    {
        var db = Database;
        if (db is null)
        {
            return;
        }
        try
        {
            var ttl = GetRefreshTokenTtl(rememberMe);
            var payload = JsonSerializer.Serialize(new { createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), rememberMe });
            await db.StringSetAsync($"{RefreshTokenPrefix}{userId}:{tokenId}", payload, TimeSpan.FromSeconds(ttl));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to store refresh token", ex);
        }
    }

    private async Task<bool> IsRefreshTokenValidAsync(string userId, string tokenId)
    {
        var db = Database;
        if (db is null)
        {
            return false;
        }
        try
        {
            return await db.KeyExistsAsync($"{RefreshTokenPrefix}{userId}:{tokenId}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to validate refresh token", ex);
            return false;
        }
    }

    private async Task RemoveRefreshTokenAsync(string userId, string tokenId)
    {
        var db = Database;
        if (db is null)
        {
            return;
        }
        try
        {
            await db.KeyDeleteAsync($"{RefreshTokenPrefix}{userId}:{tokenId}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to remove refresh token", ex);
        }
    }

    private async Task InvalidateAllUserRefreshTokensAsync(string userId)
    {
        if (!_redis.IsConnected)
        {
            return;
        }
        try
        {
            var db = _redis.GetDatabase();
            var keys = new List<RedisKey>();
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                await foreach (var key in server.KeysAsync(pattern: $"{RefreshTokenPrefix}{userId}:*"))
                {
                    keys.Add(key);
                }
            }
            if (keys.Count > 0)
            {
                await db.KeyDeleteAsync(keys.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to invalidate user refresh tokens", ex);
        }
    }

    private static int GetRefreshTokenTtl(bool rememberMe) => rememberMe ? LongSessionTtlSeconds : ShortSessionTtlSeconds;
}
