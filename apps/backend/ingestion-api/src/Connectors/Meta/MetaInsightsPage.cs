using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.Meta;

public sealed record MetaInsightsPage
{
    [JsonPropertyName("data")]
    public IReadOnlyList<JsonObject> Data { get; init; } = [];

    [JsonPropertyName("paging")]
    public MetaPaging? Paging { get; init; }
}
