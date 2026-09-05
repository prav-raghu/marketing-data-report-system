namespace IngestionApi.Connectors.Meta;

public sealed record MetaOptions
{
    public required Uri BaseAddress { get; init; }
    public string ApiVersion { get; init; } = "v21.0";
    public int PageSize { get; init; } = 500;
    public int MaxPollAttempts { get; init; } = 60;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxRetries { get; init; } = 4;
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);
}
