using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.Meta;

public sealed record MetaAsyncJobStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("async_status")]
    public string? AsyncStatus { get; init; }

    [JsonPropertyName("async_percent_completion")]
    public int AsyncPercentCompletion { get; init; }
}
