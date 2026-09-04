using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public sealed record ExtractionRequest
{
    public required string RunId { get; init; }
    public required string SourceSystem { get; init; }
    public required string SourceEntity { get; init; }
    public required string ContractVersion { get; init; }
    public required string AccountId { get; init; }
    public required ExtractionWindow Window { get; init; }
    public required AccountTier Tier { get; init; }
    public IReadOnlyList<string> Breakdowns { get; init; } = [];
    public string? Cursor { get; init; }
}
