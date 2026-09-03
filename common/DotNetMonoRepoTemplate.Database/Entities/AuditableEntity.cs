namespace DotNetMonoRepoTemplate.Database.Entities;

public abstract class AuditableEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; } = "SYSTEM";
    public string? ModifiedBy { get; set; } = "SYSTEM";
}
