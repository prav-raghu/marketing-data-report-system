namespace ApiGateway.Configuration;

public sealed record ApiGatewayOptions
{
    public required string NodeEnv { get; init; }
    public required int Port { get; init; }
    public required string CorsOrigin { get; init; }
    public required string CustomerApiUrl { get; init; }
    public required string AdminApiUrl { get; init; }
    public required string SchedulerApiUrl { get; init; }
    public required int RateLimitMax { get; init; }
    public required string RateLimitTimeWindow { get; init; }
    public bool GraphQlEnabled { get; init; }
    public string GraphQlPath { get; init; } = "/graphql";
    public bool GraphQlIntrospection { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
