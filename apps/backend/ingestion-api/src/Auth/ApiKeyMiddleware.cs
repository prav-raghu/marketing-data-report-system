using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using IngestionApi.Configuration;

namespace IngestionApi.Auth;

public sealed class ApiKeyMiddleware
{
    private const string ApiKeyPrefix = "Api-Key ";

    private readonly RequestDelegate _next;
    private readonly IngestionApiOptions _options;

    public ApiKeyMiddleware(RequestDelegate next, IngestionApiOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, IHostEnvironment environment)
    {
        if (IsPublic(context) || IsDocsInDevelopment(context, environment))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        var key = header.StartsWith(ApiKeyPrefix, StringComparison.Ordinal) ? header[ApiKeyPrefix.Length..].Trim() : null;

        if (!IsValidKey(key))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private bool IsValidKey(string? key)
    {
        if (key is null || key.Length != _options.IngestionApiKey.Length)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(_options.IngestionApiKey));
    }

    private static bool IsPublic(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null;

    private static bool IsDocsInDevelopment(HttpContext context, IHostEnvironment environment) =>
        !environment.IsProduction() && context.Request.Path.StartsWithSegments("/docs");
}
