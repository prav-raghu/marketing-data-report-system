namespace ApiGateway.Health;

public static class HealthEndpoints
{
    private static readonly HashSet<string> AllowedServices = new(StringComparer.Ordinal)
    {
        "customer-api",
        "admin-api",
        "schedule-api",
    };

    public static void MapServiceHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/services", async (HealthService healthService, CancellationToken cancellationToken) =>
        {
            var result = await healthService.CheckAllServicesAsync(cancellationToken);
            return Results.Json(result, statusCode: StatusCodeFor(result.Status));
        }).AllowAnonymous();

        app.MapGet(
            "/health/services/{serviceName}",
            async (string serviceName, HealthService healthService, CancellationToken cancellationToken) =>
            {
                if (!AllowedServices.Contains(serviceName))
                {
                    return Results.Json(new { error = "Service not found" }, statusCode: StatusCodes.Status404NotFound);
                }

                var result = await healthService.CheckServiceByNameAsync(serviceName, cancellationToken);
                if (result is null)
                {
                    return Results.Json(new { error = "Service not found" }, statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Json(result, statusCode: StatusCodeFor(result.Status));
            }).AllowAnonymous();
    }

    private static int StatusCodeFor(string status) => status switch
    {
        HealthStatusValue.Healthy => StatusCodes.Status200OK,
        HealthStatusValue.Degraded => 207,
        _ => StatusCodes.Status503ServiceUnavailable,
    };
}
