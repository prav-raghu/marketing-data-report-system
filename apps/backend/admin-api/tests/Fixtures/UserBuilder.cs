using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;

namespace AdminApi.Tests.Fixtures;

public static class UserBuilder
{
    public static User Build(Action<User>? configure = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = $"user-{Guid.NewGuid():N}",
            Email = $"user-{Guid.NewGuid():N}@test.com",
            Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1"),
            IpAddress = "127.0.0.1",
            UserStatusId = "status-placeholder",
            RoleId = "role-placeholder",
            AllowEmailCommunications = false,
        };
        configure?.Invoke(user);
        return user;
    }

    public static async Task<User> CreateAsync(AppDbContext db, Action<User>? configure = null)
    {
        var user = Build(configure);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
