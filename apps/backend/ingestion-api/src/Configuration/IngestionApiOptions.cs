namespace IngestionApi.Configuration;

public sealed record IngestionApiOptions
{
    public required string IngestionApiKey { get; init; }
    public required string RedisUrl { get; init; }
    public required string CorsOrigin { get; init; }
    public required string RateLimitWindow { get; init; }
    public required int RateLimitMax { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }
    public required string RawZoneConnectionString { get; init; }
    public required string RawZoneContainer { get; init; }
    public required string ReportingTimezone { get; init; }
    public required string ReportingCurrency { get; init; }
    public required int MaxConcurrentExtractions { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
