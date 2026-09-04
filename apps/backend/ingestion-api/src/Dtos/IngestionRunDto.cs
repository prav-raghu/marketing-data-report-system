using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Dtos;

public sealed record IngestionRunDto
{
    public required string Id { get; init; }
    public required string SourceConnectorId { get; init; }
    public required IngestionRunStatus Status { get; init; }
    public required IngestionRunTrigger Trigger { get; init; }
    public required DateOnly WindowStart { get; init; }
    public required DateOnly WindowEnd { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required int RecordCount { get; init; }
    public required int PartCount { get; init; }
    public required long CompressedBytes { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
