using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.TikTok;

public sealed record TikTokPageInfo
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; init; }

    [JsonPropertyName("total_number")]
    public int TotalNumber { get; init; }

    [JsonPropertyName("total_page")]
    public int TotalPage { get; init; }
}
