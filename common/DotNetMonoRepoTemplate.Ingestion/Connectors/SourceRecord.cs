using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public sealed record SourceRecord
{
    public required string IdempotencyKey { get; init; }
    public required PayloadFormat PayloadFormat { get; init; }
    public required JsonNode Payload { get; init; }
    public required DateTime ExtractedAtUtc { get; init; }
    public DateTime? SourceWatermarkUtc { get; init; }
    public string? RawArtifactPath { get; init; }
}
