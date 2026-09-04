using System.Text.Json;
using DotNetMonoRepoTemplate.Logging;

namespace IngestionApi.Middleware;

public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> SkipUrls = new(StringComparer.OrdinalIgnoreCase) { "/health", "/ready" };

    private readonly RequestDelegate _next;
    private readonly Logger _logger = new("RequestLogger");

    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (context.Request.Method == HttpMethods.Options || ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Request.Headers["x-correlation-id"].FirstOrDefault();
        _logger.Info(
            "Incoming request",
            new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["url"] = path,
                ["correlationId"] = correlationId,
            });

        var startTime = DateTime.UtcNow;
        await _next(context);
        var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.Info(
            "Outgoing response",
            new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["url"] = path,
                ["statusCode"] = context.Response.StatusCode,
                ["durationMs"] = durationMs,
                ["correlationId"] = correlationId,
            });
    }

    private static bool ShouldSkip(string url) =>
        url.StartsWith("/docs", StringComparison.OrdinalIgnoreCase) || SkipUrls.Contains(url);
}
