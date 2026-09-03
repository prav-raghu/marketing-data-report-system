using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DotNetMonoRepoTemplate.Cache;

public static class CacheServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetMonoRepoTemplateCache(
        this IServiceCollection services,
        string redisUrl,
        bool tlsRejectUnauthorized = true)
    {
        if (string.IsNullOrWhiteSpace(redisUrl))
        {
            throw new ArgumentException(
                "REDIS_URL is required - no discrete REDIS_HOST/REDIS_PORT fallback is supported",
                nameof(redisUrl));
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configurationOptions = ConfigurationOptions.Parse(redisUrl);
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.ConnectRetry = 3;
            configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(100, 2000);
            if (configurationOptions.Ssl && !tlsRejectUnauthorized)
            {
                configurationOptions.CertificateValidation += (_, _, _, _) => true;
            }
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<RedisCacheService>();
        return services;
    }
}
