using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.Meta;

public sealed record MetaCursors
{
    [JsonPropertyName("before")]
    public string? Before { get; init; }

    [JsonPropertyName("after")]
    public string? After { get; init; }
}
