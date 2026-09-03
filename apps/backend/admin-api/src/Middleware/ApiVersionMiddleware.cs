using System.Text.RegularExpressions;

namespace AdminApi.Middleware;

public sealed partial class ApiVersionMiddleware
{
    private const string CurrentVersion = "v1";
    private static readonly string[] SupportedVersions = { "v1", "v2" };

    private readonly RequestDelegate _next;

    public ApiVersionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var versionFromHeader = context.Request.Headers["api-version"].FirstOrDefault();
        var versionFromAccept = ExtractVersionFromAccept(context.Request.Headers.Accept.FirstOrDefault());
        var versionFromUrl = ExtractVersionFromUrl(context.Request.Path.Value ?? string.Empty);

        var detectedVersion = versionFromHeader ?? versionFromAccept ?? versionFromUrl ?? CurrentVersion;

        if (!SupportedVersions.Contains(detectedVersion))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "Unsupported API version",
                supportedVersions = SupportedVersions,
            });
            return;
        }

        context.Items["ApiVersion"] = detectedVersion;
        context.Response.Headers["X-API-Version"] = detectedVersion;

        await _next(context);
    }

    private static string? ExtractVersionFromAccept(string? accept)
    {
        if (string.IsNullOrEmpty(accept))
        {
            return null;
        }
        var match = AcceptVersionRegex().Match(accept);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractVersionFromUrl(string url)
    {
        var match = UrlVersionRegex().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"application/vnd\.api\.(v\d+)\+json")]
    private static partial Regex AcceptVersionRegex();

    [GeneratedRegex(@"/api/(v\d+)/")]
    private static partial Regex UrlVersionRegex();
}
