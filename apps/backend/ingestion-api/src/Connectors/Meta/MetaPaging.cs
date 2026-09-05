using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.Meta;

public sealed record MetaPaging
{
    [JsonPropertyName("cursors")]
    public MetaCursors? Cursors { get; init; }

    [JsonPropertyName("next")]
    public string? Next { get; init; }
}
