using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Ingestion.Tiering;

public sealed record TierPolicy
{
    public required AccountTier Tier { get; init; }
    public required int RestatementDays { get; init; }
    public required TimeSpan Cadence { get; init; }
    public required IReadOnlyList<string> Breakdowns { get; init; }

    private static readonly TierPolicy Tier1Policy = new()
    {
        Tier = AccountTier.Tier1,
        RestatementDays = 28,
        Cadence = TimeSpan.FromHours(1),
        Breakdowns = [BreakdownName.Geo, BreakdownName.Device, BreakdownName.Placement, BreakdownName.AgeGender],
    };

    private static readonly TierPolicy Tier2Policy = new()
    {
        Tier = AccountTier.Tier2,
        RestatementDays = 14,
        Cadence = TimeSpan.FromDays(1),
        Breakdowns = [BreakdownName.Geo, BreakdownName.Device],
    };

    private static readonly TierPolicy Tier3Policy = new()
    {
        Tier = AccountTier.Tier3,
        RestatementDays = 7,
        Cadence = TimeSpan.FromDays(1),
        Breakdowns = [],
    };

    public static TierPolicy For(AccountTier tier) => tier switch
    {
        AccountTier.Tier1 => Tier1Policy,
        AccountTier.Tier2 => Tier2Policy,
        AccountTier.Tier3 => Tier3Policy,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown account tier"),
    };
}
