namespace Cms.Configuration;

public sealed record CmsOptions
{
    public required string DatabaseUrl { get; init; }
    public required int Port { get; init; }
    public required string NodeEnv { get; init; }

    public bool IsProduction => string.Equals(NodeEnv, "production", StringComparison.OrdinalIgnoreCase);
}
