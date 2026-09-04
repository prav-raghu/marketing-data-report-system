namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class SourceSystem : AuditableEntity
{
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public required string Vendor { get; set; }
    public required string ReportingTimezone { get; set; }
    public required string DefaultCurrency { get; set; }
    public string? OwnerEmail { get; set; }
    public ICollection<SourceConnector> Connectors { get; set; } = new List<SourceConnector>();
    public ICollection<SchemaContract> Contracts { get; set; } = new List<SchemaContract>();
}
