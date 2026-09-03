namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class UserStatus : AuditableEntity
{
    public required string Name { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
