using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Services;

public static class TierAssignment
{
    private const double Tier1Share = 0.10;
    private const double Tier2Share = 0.30;

    public static AccountTier Assign(int rank, int total, decimal trailingSpend)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rank);
        ArgumentOutOfRangeException.ThrowIfLessThan(total, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rank, total);

        if (trailingSpend <= 0m)
        {
            return AccountTier.Tier3;
        }

        var tier1Count = Math.Max(1, (int)Math.Ceiling(total * Tier1Share));
        var tier2Count = (int)Math.Ceiling(total * Tier2Share);

        if (rank < tier1Count)
        {
            return AccountTier.Tier1;
        }

        return rank < tier1Count + tier2Count ? AccountTier.Tier2 : AccountTier.Tier3;
    }
}
