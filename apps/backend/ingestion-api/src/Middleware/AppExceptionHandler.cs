using Microsoft.AspNetCore.Diagnostics;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Observability;

namespace IngestionApi.Middleware;

public sealed class AppExceptionHandler : IExceptionHandler
{
    private const string InternalServerError = "Internal server error";

    private readonly Logger _logger = new("ErrorHandler");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.Error("Unhandled request error", exception);
        SentryCapture.CaptureException(exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(
            new { isSuccessful = false, message = InternalServerError },
            cancellationToken);

        return true;
    }
}
