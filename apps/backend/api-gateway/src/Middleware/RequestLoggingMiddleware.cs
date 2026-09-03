using System.Text;
using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Logging;

namespace ApiGateway.Middleware;

public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> SkipUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/ready",
        "/health/live",
        "/health/ready",
        "/health/services",
    };

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

        if (context.Request.Method != HttpMethods.Get && context.Request.ContentLength is > 0)
        {
            await LogRequestBodyAsync(context);
        }

        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        var startTime = DateTime.UtcNow;
        await _next(context);
        var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        responseBuffer.Seek(0, SeekOrigin.Begin);
        var payload = await new StreamReader(responseBuffer, Encoding.UTF8).ReadToEndAsync();
        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalResponseBody);
        context.Response.Body = originalResponseBody;

        var logData = new Dictionary<string, object?>
        {
            ["method"] = context.Request.Method,
            ["url"] = path,
            ["statusCode"] = context.Response.StatusCode,
            ["durationMs"] = durationMs,
            ["correlationId"] = correlationId,
        };

        if (payload.Length > 0)
        {
            logData["response"] = SensitiveDataMasker.TryParseAndMask(payload)?.ToJsonString();
        }

        _logger.Info("Outgoing response", logData);
    }

    private async Task LogRequestBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (body.Length == 0)
        {
            return;
        }

        var masked = SensitiveDataMasker.TryParseAndMask(body);
        _logger.Debug("Request body", new Dictionary<string, object?> { ["body"] = masked?.ToJsonString() });
    }

    private static bool ShouldSkip(string url) =>
        url.StartsWith("/docs", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
        || SkipUrls.Contains(url);
}
