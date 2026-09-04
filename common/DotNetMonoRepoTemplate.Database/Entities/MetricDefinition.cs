using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class MetricDefinition : AuditableEntity
{
    public required string CanonicalName { get; set; }
    public required string DisplayName { get; set; }
    public MetricAdditivity Additivity { get; set; } = MetricAdditivity.Additive;
    public bool IsComparableAcrossPlatforms { get; set; }
    public required string Definition { get; set; }
    public string? NonComparabilityReason { get; set; }
}
