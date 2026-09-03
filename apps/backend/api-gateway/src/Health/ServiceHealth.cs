namespace ApiGateway.Health;

public static class HealthStatusValue
{
    public const string Healthy = "healthy";
    public const string Unhealthy = "unhealthy";
    public const string Degraded = "degraded";
}

public sealed record ServiceHealth
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Status { get; init; }
    public double? ResponseTimeMs { get; init; }
    public string? Error { get; init; }
}

public sealed record GatewayInfo
{
    public required double UptimeSeconds { get; init; }
}

public sealed record HealthCheckResponse
{
    public required string Status { get; init; }
    public required string Timestamp { get; init; }
    public required GatewayInfo Gateway { get; init; }
    public required IReadOnlyList<ServiceHealth> Services { get; init; }
}
