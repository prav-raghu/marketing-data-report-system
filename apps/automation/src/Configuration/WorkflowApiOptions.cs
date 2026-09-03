namespace WorkflowApi.Configuration;

public sealed record WorkflowApiOptions
{
    public required string DatabaseUrl { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }
    public required string CorsOrigin { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
