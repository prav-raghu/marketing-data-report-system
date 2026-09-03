using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetMonoRepoTemplate.Metrics;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = ToStatusText(report.Status),
            timestamp = DateTime.UtcNow.ToString("O"),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = ToStatusText(entry.Value.Status),
                    latency = entry.Value.Duration.TotalMilliseconds,
                    message = entry.Value.Description,
                }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static string ToStatusText(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy",
    };
}
