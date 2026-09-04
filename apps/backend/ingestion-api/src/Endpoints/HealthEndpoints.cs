using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Logging;
using StackExchange.Redis;

namespace IngestionApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "pong" })).AllowAnonymous();

        app.MapGet("/api/v1/ready", async (AppDbContext db, IConnectionMultiplexer redis) =>
        {
            var logger = new Logger("HealthEndpoints");
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1");
                if (redis.IsConnected)
                {
                    await redis.GetDatabase().PingAsync();
                }
                return Results.Ok(new { status = "ready", db = "ok", redis = "ok" });
            }
            catch (Exception ex)
            {
                logger.Error("Readiness check failed", ex);
                return Results.Json(
                    new { status = "unavailable", reason = "Service dependencies unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowAnonymous();

        app.MapGet("/api/v2/health", () => Results.Ok(new { status = "healthy", version = "v2" })).AllowAnonymous();
    }
}
