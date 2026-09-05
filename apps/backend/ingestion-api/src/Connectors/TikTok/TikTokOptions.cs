namespace IngestionApi.Connectors.TikTok;

public sealed record TikTokOptions
{
    public required Uri BaseAddress { get; init; }
    public int PageSize { get; init; } = 1000;
    public int MaxRetries { get; init; } = 4;
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
}
