using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class IngestionRun : AuditableEntity
{
    public required string SourceConnectorId { get; set; }
    public SourceConnector? SourceConnector { get; set; }
    public IngestionRunStatus Status { get; set; } = IngestionRunStatus.Pending;
    public IngestionRunTrigger Trigger { get; set; } = IngestionRunTrigger.Scheduled;
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RecordCount { get; set; }
    public int PartCount { get; set; }
    public long CompressedBytes { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredBy { get; set; }
    public ICollection<QuarantineRecord> QuarantineRecords { get; set; } = new List<QuarantineRecord>();
}
