using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.TikTok;

public sealed record TikTokReportData
{
    [JsonPropertyName("page_info")]
    public TikTokPageInfo? PageInfo { get; init; }

    [JsonPropertyName("list")]
    public IReadOnlyList<TikTokReportRow> List { get; init; } = [];
}
