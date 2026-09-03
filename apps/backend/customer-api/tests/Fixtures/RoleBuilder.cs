using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Tests.Fixtures;

public static class RoleBuilder
{
    public static Role Build(Action<Role>? configure = null)
    {
        var role = new Role
        {
            Id = Guid.NewGuid().ToString(),
            Name = RoleName.ChatUser,
        };
        configure?.Invoke(role);
        return role;
    }

    public static async Task<Role> CreateAsync(AppDbContext db, Action<Role>? configure = null)
    {
        var role = Build(configure);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }
}
