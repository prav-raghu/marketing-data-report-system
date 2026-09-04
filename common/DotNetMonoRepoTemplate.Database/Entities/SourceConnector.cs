using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class SourceConnector : AuditableEntity
{
    public required string SourceSystemId { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public required string SourceEntity { get; set; }
    public required string ContractVersion { get; set; }
    public required string AccountId { get; set; }
    public AccountTier Tier { get; set; } = AccountTier.Tier3;
    public int RestatementDays { get; set; } = 7;
    public List<string> Breakdowns { get; set; } = new();
    public required string CronSchedule { get; set; }
    public decimal TrailingNinetyDaySpendZar { get; set; }
    public DateTime? TierEvaluatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public int FreshnessSlaMinutes { get; set; } = 1440;
    public ICollection<IngestionRun> Runs { get; set; } = new List<IngestionRun>();
}
