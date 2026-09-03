using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetMonoRepoTemplate.Metrics;

public sealed class DelegateHealthCheck : IHealthCheck
{
    private readonly Func<CancellationToken, Task<bool>> _check;
    private readonly string _healthyMessage;
    private readonly string _unhealthyMessage;
    private readonly HealthStatus _failureStatus;

    public DelegateHealthCheck(
        Func<CancellationToken, Task<bool>> check,
        string healthyMessage,
        string unhealthyMessage,
        HealthStatus failureStatus)
    {
        _check = check;
        _healthyMessage = healthyMessage;
        _unhealthyMessage = unhealthyMessage;
        _failureStatus = failureStatus;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _check(cancellationToken);
            return isHealthy
                ? HealthCheckResult.Healthy(_healthyMessage)
                : new HealthCheckResult(_failureStatus, _unhealthyMessage);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(_failureStatus, ex.Message, ex);
        }
    }
}
