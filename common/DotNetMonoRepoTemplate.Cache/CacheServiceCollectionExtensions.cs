using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DotNetMonoRepoTemplate.Cache;

public static class CacheServiceCollectionExtensions
{
    [SuppressMessage(
        "Security",
        "CA5359:DoNotDisableCertificateValidation",
        Justification = "Caller must explicitly pass tlsRejectUnauthorized: false to reach this path - " +
            "intended only for self-signed certs in local/dev Redis deployments, never the default.")]
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
                configurationOptions.CertificateValidation += AcceptAnyCertificate;
            }
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<RedisCacheService>();
        return services;
    }

    private static bool AcceptAnyCertificate(
        object sender,
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        System.Net.Security.SslPolicyErrors sslPolicyErrors) => true;
}
