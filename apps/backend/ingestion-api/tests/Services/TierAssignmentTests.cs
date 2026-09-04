using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Services;
using Xunit;

namespace IngestionApi.Tests.Services;

public sealed class TierAssignmentTests
{
    [Theory]
    [InlineData(0, 100, AccountTier.Tier1)]
    [InlineData(9, 100, AccountTier.Tier1)]
    [InlineData(10, 100, AccountTier.Tier2)]
    [InlineData(39, 100, AccountTier.Tier2)]
    [InlineData(40, 100, AccountTier.Tier3)]
    [InlineData(99, 100, AccountTier.Tier3)]
    public void Assign_PlacesRankInExpectedTier(int rank, int total, AccountTier expected)
    {
        TierAssignment.Assign(rank, total, trailingSpend: 5_000m).Should().Be(expected);
    }

    [Fact]
    public void Assign_WithSingleConnector_PlacesItInTier1()
    {
        TierAssignment.Assign(rank: 0, total: 1, trailingSpend: 1m).Should().Be(AccountTier.Tier1);
    }

    [Fact]
    public void Assign_WithZeroSpend_AlwaysReturnsTier3()
    {
        TierAssignment.Assign(rank: 0, total: 10, trailingSpend: 0m).Should().Be(AccountTier.Tier3);
    }

    [Fact]
    public void Assign_WithNegativeSpend_AlwaysReturnsTier3()
    {
        TierAssignment.Assign(rank: 0, total: 10, trailingSpend: -25m).Should().Be(AccountTier.Tier3);
    }

    [Fact]
    public void Assign_WithRankOutsideTotal_Throws()
    {
        var act = () => TierAssignment.Assign(rank: 10, total: 10, trailingSpend: 1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
