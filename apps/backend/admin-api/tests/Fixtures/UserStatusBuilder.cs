using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;

namespace AdminApi.Tests.Fixtures;

public static class UserStatusBuilder
{
    public static UserStatus Build(Action<UserStatus>? configure = null)
    {
        var status = new UserStatus
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Online",
        };
        configure?.Invoke(status);
        return status;
    }

    public static async Task<UserStatus> CreateAsync(AppDbContext db, Action<UserStatus>? configure = null)
    {
        var status = Build(configure);
        db.UserStatuses.Add(status);
        await db.SaveChangesAsync();
        return status;
    }
}
