using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Services;
using IngestionApi.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngestionApi.Tests.Services;

public sealed class AccountTierServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 2, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecalculateAsync_RanksBySpendAndAppliesTheTierPolicy()
    {
        await using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 10; i++)
        {
            db.SourceConnectors.Add(new SourceConnectorBuilder()
                .WithAccountId($"act-{i}")
                .WithTrailingSpend((10 - i) * 1_000m)
                .WithTier(AccountTier.Tier3)
                .Build());
        }

        await db.SaveChangesAsync();
        var service = new AccountTierService(db, new FixedTimeProvider(FixedUtcNow));

        await service.RecalculateAsync(CancellationToken.None);

        var ordered = await db.SourceConnectors
            .OrderByDescending(c => c.TrailingNinetyDaySpendZar)
            .ToListAsync();

        ordered[0].Tier.Should().Be(AccountTier.Tier1);
        ordered[0].RestatementDays.Should().Be(28);
        ordered[0].Breakdowns.Should().HaveCount(4);

        ordered[1].Tier.Should().Be(AccountTier.Tier2);
        ordered[1].RestatementDays.Should().Be(14);
        ordered[1].Breakdowns.Should().BeEquivalentTo(new[] { BreakdownName.Geo, BreakdownName.Device });

        ordered[9].Tier.Should().Be(AccountTier.Tier3);
        ordered[9].RestatementDays.Should().Be(7);
        ordered[9].Breakdowns.Should().BeEmpty();
    }

    [Fact]
    public async Task RecalculateAsync_CountsEveryConnectorWhoseTierMoved()
    {
        await using var db = TestDbContextFactory.Create();
        db.SourceConnectors.Add(new SourceConnectorBuilder()
            .WithAccountId("act-high")
            .WithTrailingSpend(500_000m)
            .WithTier(AccountTier.Tier3)
            .Build());
        db.SourceConnectors.Add(new SourceConnectorBuilder()
            .WithAccountId("act-low")
            .WithTrailingSpend(10m)
            .WithTier(AccountTier.Tier3)
            .Build());
        await db.SaveChangesAsync();

        var service = new AccountTierService(db, new FixedTimeProvider(FixedUtcNow));

        var changed = await service.RecalculateAsync(CancellationToken.None);

        changed.Should().Be(2);

        var connectors = await db.SourceConnectors
            .OrderByDescending(c => c.TrailingNinetyDaySpendZar)
            .ToListAsync();
        connectors[0].Tier.Should().Be(AccountTier.Tier1);
        connectors[1].Tier.Should().Be(AccountTier.Tier2);
    }

    [Fact]
    public async Task RecalculateAsync_LeavesZeroSpendConnectorsInTier3()
    {
        await using var db = TestDbContextFactory.Create();
        db.SourceConnectors.Add(new SourceConnectorBuilder()
            .WithAccountId("act-dormant")
            .WithTrailingSpend(0m)
            .WithTier(AccountTier.Tier1)
            .Build());
        await db.SaveChangesAsync();

        var service = new AccountTierService(db, new FixedTimeProvider(FixedUtcNow));

        await service.RecalculateAsync(CancellationToken.None);

        var connector = await db.SourceConnectors.SingleAsync();
        connector.Tier.Should().Be(AccountTier.Tier3);
        connector.TierEvaluatedAt.Should().Be(FixedUtcNow.UtcDateTime);
    }

    [Fact]
    public async Task RecalculateAsync_WithNoConnectors_ReturnsZero()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new AccountTierService(db, new FixedTimeProvider(FixedUtcNow));

        var changed = await service.RecalculateAsync(CancellationToken.None);

        changed.Should().Be(0);
    }
}
