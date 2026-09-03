using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DotNetMonoRepoTemplate.Database;

namespace ScheduleApi.Tests.Fixtures;

public static class TestDbContextFactory
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
