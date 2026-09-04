namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class IngestionCheckpoint : AuditableEntity
{
    public required string SourceConnectorId { get; set; }
    public SourceConnector? SourceConnector { get; set; }
    public DateTime? Watermark { get; set; }
    public string? Cursor { get; set; }
    public string? LastSuccessfulRunId { get; set; }
}
