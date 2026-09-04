using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class QuarantineRecord : AuditableEntity
{
    public required string IngestionRunId { get; set; }
    public IngestionRun? IngestionRun { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public required string EnvelopePath { get; set; }
    public QuarantineResolution Resolution { get; set; } = QuarantineResolution.Unresolved;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
