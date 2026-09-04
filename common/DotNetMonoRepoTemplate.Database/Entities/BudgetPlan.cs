namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class BudgetPlan : AuditableEntity
{
    public required string SourceSystemId { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public required string CampaignId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal PlannedSpendZar { get; set; }
    public string? UploadedBy { get; set; }
}
