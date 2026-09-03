namespace ScheduleApi.Configuration;

public sealed record ScheduleApiOptions
{
    public required string ScheduleApiKey { get; init; }
    public string? JwtSecret { get; init; }
    public required string RedisUrl { get; init; }
    public required string RefreshTokenExpiry { get; init; }
    public required string AuthTokenExpiry { get; init; }
    public required string CorsOrigin { get; init; }
    public required string RateLimitWindow { get; init; }
    public required int RateLimitMax { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }
    public int? AccountBanThreshold { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
