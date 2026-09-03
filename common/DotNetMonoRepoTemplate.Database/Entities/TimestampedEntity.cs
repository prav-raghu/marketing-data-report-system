namespace DotNetMonoRepoTemplate.Database.Entities;

public abstract class TimestampedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
