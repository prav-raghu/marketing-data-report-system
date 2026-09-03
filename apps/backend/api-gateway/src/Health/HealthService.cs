using ApiGateway.Configuration;
using DotNetMonoRepoTemplate.Logging;

namespace ApiGateway.Health;

public sealed class HealthService
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly DateTime ProcessStartTime = DateTime.UtcNow;

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<(string Name, string Url)> _serviceEndpoints;
    private readonly Logger _logger = new(nameof(HealthService));

    public HealthService(HttpClient httpClient, ApiGatewayOptions options)
    {
        _httpClient = httpClient;
        _serviceEndpoints = new[]
        {
            ("customer-api", options.CustomerApiUrl),
            ("admin-api", options.AdminApiUrl),
        };
    }

    public static double UptimeSeconds => (DateTime.UtcNow - ProcessStartTime).TotalSeconds;

    public async Task<HealthCheckResponse> CheckAllServicesAsync(CancellationToken cancellationToken)
    {
        var checks = await Task.WhenAll(_serviceEndpoints.Select(service => CheckServiceAsync(service, cancellationToken)));

        var unhealthyCount = checks.Count(s => s.Status == HealthStatusValue.Unhealthy);
        var degradedCount = checks.Count(s => s.Status == HealthStatusValue.Degraded);

        var overallStatus = HealthStatusValue.Healthy;
        if (unhealthyCount == checks.Length)
        {
            overallStatus = HealthStatusValue.Unhealthy;
        }
        else if (unhealthyCount > 0 || degradedCount > 0)
        {
            overallStatus = HealthStatusValue.Degraded;
        }

        return new HealthCheckResponse
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Gateway = new GatewayInfo { UptimeSeconds = UptimeSeconds },
            Services = checks,
        };
    }

    public async Task<ServiceHealth?> CheckServiceByNameAsync(string serviceName, CancellationToken cancellationToken)
    {
        var service = _serviceEndpoints.FirstOrDefault(s => s.Name == serviceName);
        return service.Name is null ? null : await CheckServiceAsync(service, cancellationToken);
    }

    private async Task<ServiceHealth> CheckServiceAsync((string Name, string Url) service, CancellationToken cancellationToken)
    {
        var healthUrl = $"{service.Url}/health";
        var startTime = DateTime.UtcNow;

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(HealthCheckTimeout);

            using var response = await _httpClient.GetAsync(healthUrl, timeoutSource.Token);
            var responseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                return new ServiceHealth
                {
                    Name = service.Name,
                    Url = service.Url,
                    Status = responseTimeMs > 3000 ? HealthStatusValue.Degraded : HealthStatusValue.Healthy,
                    ResponseTimeMs = responseTimeMs,
                };
            }

            _logger.Warn(
                "Service health check failed",
                new Dictionary<string, object?> { ["service"] = service.Name, ["statusCode"] = (int)response.StatusCode });

            return new ServiceHealth
            {
                Name = service.Name,
                Url = service.Url,
                Status = HealthStatusValue.Unhealthy,
                ResponseTimeMs = responseTimeMs,
                Error = $"HTTP {(int)response.StatusCode}",
            };
        }
        catch (Exception ex)
        {
            var responseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.Error("Service health check error", ex);

            return new ServiceHealth
            {
                Name = service.Name,
                Url = service.Url,
                Status = HealthStatusValue.Unhealthy,
                ResponseTimeMs = responseTimeMs,
                Error = ex.Message,
            };
        }
    }
}
