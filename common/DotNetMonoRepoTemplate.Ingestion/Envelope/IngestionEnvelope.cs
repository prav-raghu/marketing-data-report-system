using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Ingestion.Envelope;

public sealed record IngestionEnvelope
{
    public required string EnvelopeVersion { get; init; }
    public required string SourceSystem { get; init; }
    public required string SourceEntity { get; init; }
    public required string ContractVersion { get; init; }
    public required string RunId { get; init; }
    public required int BatchSequence { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTime ExtractedAtUtc { get; init; }
    public required DateTime IngestedAtUtc { get; init; }
    public DateTime? SourceWatermarkUtc { get; init; }
    public required PayloadFormat PayloadFormat { get; init; }
    public required string PayloadHash { get; init; }
    public required JsonNode Payload { get; init; }
    public string? RawArtifactPath { get; init; }
}
