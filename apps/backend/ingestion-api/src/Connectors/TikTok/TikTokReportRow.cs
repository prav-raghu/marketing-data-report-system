using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.TikTok;

public sealed record TikTokReportRow
{
    [JsonPropertyName("dimensions")]
    public JsonObject? Dimensions { get; init; }

    [JsonPropertyName("metrics")]
    public JsonObject? Metrics { get; init; }
}
