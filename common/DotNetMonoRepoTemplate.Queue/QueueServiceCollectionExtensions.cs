using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetMonoRepoTemplate.Queue;

public static class QueueServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateQueue(this IServiceCollection services, string redisUrl)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseRedisStorage(redisUrl));

        services.AddHangfireServer();

        return services;
    }
}
