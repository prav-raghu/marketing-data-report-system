using System.Text.Json.Serialization;

namespace IngestionApi.Connectors.Meta;

public sealed record MetaAsyncJobSubmission
{
    [JsonPropertyName("report_run_id")]
    public string? ReportRunId { get; init; }
}
