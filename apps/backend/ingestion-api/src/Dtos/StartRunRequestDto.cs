using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Dtos;

public sealed record StartRunRequestDto
{
    public required string SourceConnectorId { get; init; }
    public IngestionRunTrigger Trigger { get; init; } = IngestionRunTrigger.Scheduled;
    public DateOnly? AsOfDate { get; init; }
    public DateOnly? WindowStart { get; init; }
    public DateOnly? WindowEnd { get; init; }
    public string? TriggeredBy { get; init; }
}
