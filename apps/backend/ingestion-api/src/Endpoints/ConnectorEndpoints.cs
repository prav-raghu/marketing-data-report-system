using DotNetMonoRepoTemplate.Database;
using IngestionApi.Services;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Endpoints;

public static class ConnectorEndpoints
{
    public static void MapConnectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/v1/connectors");

        group.MapGet("/", async (AppDbContext db, IConnectorRegistry registry, CancellationToken cancellationToken) =>
        {
            var connectors = await db.SourceConnectors
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.SourceSystemId)
                .ThenBy(c => c.AccountId)
                .Select(c => new
                {
                    c.Id,
                    c.SourceSystemId,
                    c.SourceEntity,
                    c.AccountId,
                    c.ContractVersion,
                    c.Tier,
                    c.RestatementDays,
                    c.Breakdowns,
                    c.CronSchedule,
                    c.LastRunAt,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { registeredConnectorKeys = registry.RegisteredKeys, connectors });
        });

        group.MapPost("/retier", async (IAccountTierService tierService, CancellationToken cancellationToken) =>
        {
            var changed = await tierService.RecalculateAsync(cancellationToken);
            return Results.Ok(new { isSuccessful = true, tierChanges = changed });
        });
    }
}
