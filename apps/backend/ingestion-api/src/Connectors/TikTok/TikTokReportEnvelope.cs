using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.TikTok;

public sealed record TikTokReportEnvelope
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }

    [JsonPropertyName("data")]
    public TikTokReportData? Data { get; init; }
}
