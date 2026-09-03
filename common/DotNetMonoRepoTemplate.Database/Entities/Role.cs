namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class Role : AuditableEntity
{
    public required string Name { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
