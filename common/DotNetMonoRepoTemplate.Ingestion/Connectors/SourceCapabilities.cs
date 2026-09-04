using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public sealed record SourceCapabilities
{
    public required PayloadFormat NativeFormat { get; init; }
    public required bool SupportsIncremental { get; init; }
    public required bool SupportsBreakdowns { get; init; }
    public required bool SupportsAttributionWindows { get; init; }
    public required int MaxRestatementDays { get; init; }
}
