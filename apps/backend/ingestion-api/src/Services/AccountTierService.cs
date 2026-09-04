using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Ingestion.Tiering;
using DotNetMonoRepoTemplate.Logging;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Services;

public sealed class AccountTierService : IAccountTierService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly Logger _logger = new("AccountTierService");

    public AccountTierService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<int> RecalculateAsync(CancellationToken cancellationToken)
    {
        var connectors = await _db.SourceConnectors
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.TrailingNinetyDaySpendZar)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (connectors.Count == 0)
        {
            return 0;
        }

        var evaluatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = 0;

        for (var rank = 0; rank < connectors.Count; rank++)
        {
            var connector = connectors[rank];
            var tier = TierAssignment.Assign(rank, connectors.Count, connector.TrailingNinetyDaySpendZar);
            var policy = TierPolicy.For(tier);

            if (connector.Tier != tier)
            {
                changed++;
            }

            connector.Tier = tier;
            connector.RestatementDays = policy.RestatementDays;
            connector.Breakdowns = policy.Breakdowns.ToList();
            connector.TierEvaluatedAt = evaluatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.Info(
            "Account tiers recalculated",
            new Dictionary<string, object?>
            {
                ["connectorCount"] = connectors.Count,
                ["tierChanges"] = changed,
            });

        return changed;
    }
}
