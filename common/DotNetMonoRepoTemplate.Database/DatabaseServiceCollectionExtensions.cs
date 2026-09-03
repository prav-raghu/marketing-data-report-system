using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetMonoRepoTemplate.Database;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateDatabase(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        return services;
    }
}
