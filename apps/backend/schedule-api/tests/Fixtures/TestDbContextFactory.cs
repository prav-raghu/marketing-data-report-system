using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;

namespace ScheduleApi.Tests.Fixtures;

public static class TestDbContextFactory
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
