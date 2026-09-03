namespace AdminApi.Configuration;

public sealed record AdminApiOptions
{
    public required string JwtSecret { get; init; }
    public required string JwtRefreshSecret { get; init; }
    public required string TwoFactorEncryptionKey { get; init; }
    public required string RedisUrl { get; init; }
    public required string RefreshTokenExpiry { get; init; }
    public required string AuthTokenExpiry { get; init; }
    public required string CorsOrigin { get; init; }
    public required string RateLimitWindow { get; init; }
    public required int RateLimitMax { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }
    public required string MailtrapApiKey { get; init; }
    public required string MailtrapFrom { get; init; }
    public required string MailtrapFromName { get; init; }
    public required string AdminWebUrl { get; init; }
    public required int PasswordResetExpirationMinutes { get; init; }
    public int? AccountBanThreshold { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
