using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class ConversionActionMapping : AuditableEntity
{
    public required string SourceSystemId { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public string AccountId { get; set; } = AccountScope.AllAccounts;
    public required string PlatformActionType { get; set; }
    public bool CountsAsConversion { get; set; }
    public string? Notes { get; set; }
}
