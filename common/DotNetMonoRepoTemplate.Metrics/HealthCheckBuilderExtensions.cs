using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetMonoRepoTemplate.Metrics;

public static class HealthCheckBuilderExtensions
{
    private static readonly string[] ReadyTag = { "ready" };

    public static IHealthChecksBuilder AddDatabaseHealthCheck(
        this IHealthChecksBuilder builder,
        Func<CancellationToken, Task<bool>> check,
        string name = "database") =>
        builder.Add(new HealthCheckRegistration(
            name,
            _ => new DelegateHealthCheck(check, "Database connection is healthy", "Database connection failed", HealthStatus.Unhealthy),
            HealthStatus.Unhealthy,
            ReadyTag));

    public static IHealthChecksBuilder AddRedisHealthCheck(
        this IHealthChecksBuilder builder,
        Func<CancellationToken, Task<bool>> check,
        string name = "redis") =>
        builder.Add(new HealthCheckRegistration(
            name,
            _ => new DelegateHealthCheck(check, "Redis connection is healthy", "Redis connection unavailable", HealthStatus.Degraded),
            HealthStatus.Degraded,
            ReadyTag));

    public static IHealthChecksBuilder AddExternalServiceHealthCheck(
        this IHealthChecksBuilder builder,
        string name,
        Func<CancellationToken, Task<bool>> check,
        bool critical = false)
    {
        var failureStatus = critical ? HealthStatus.Unhealthy : HealthStatus.Degraded;
        return builder.Add(new HealthCheckRegistration(
            name,
            _ => new DelegateHealthCheck(check, $"{name} is reachable", $"{name} is unreachable", failureStatus),
            failureStatus,
            ReadyTag));
    }
}
