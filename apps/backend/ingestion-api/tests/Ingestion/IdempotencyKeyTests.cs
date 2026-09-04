using DotNetMonoRepoTemplate.Ingestion.Keys;
using FluentAssertions;
using Xunit;

namespace IngestionApi.Tests.Ingestion;

public sealed class IdempotencyKeyTests
{
    [Fact]
    public void Create_ProducesTheDocumentedShape()
    {
        var key = IdempotencyKey.Create(
            "tiktok_ads",
            "act_884213",
            "ad",
            "1780044219",
            new DateOnly(2026, 9, 1),
            new Dictionary<string, string> { ["geo"] = "ZA", ["dev"] = "mobile" });

        key.Should().Be("tiktok_ads|act_884213|ad|1780044219|2026-09-01|dev:mobile|geo:ZA");
    }

    [Fact]
    public void Create_IsIndependentOfBreakdownInsertionOrder()
    {
        var first = IdempotencyKey.Create(
            "meta_facebook",
            "act_1",
            "ad",
            "a1",
            new DateOnly(2026, 9, 1),
            new Dictionary<string, string> { ["placement"] = "feed", ["geo"] = "ZA", ["dev"] = "mobile" });

        var second = IdempotencyKey.Create(
            "meta_facebook",
            "act_1",
            "ad",
            "a1",
            new DateOnly(2026, 9, 1),
            new Dictionary<string, string> { ["dev"] = "mobile", ["placement"] = "feed", ["geo"] = "ZA" });

        second.Should().Be(first);
    }

    [Fact]
    public void Create_WithoutBreakdowns_OmitsTheBreakdownSegment()
    {
        var key = IdempotencyKey.Create("legacy_adserver", "acct", "campaign", "BK-1", new DateOnly(2026, 9, 1));

        key.Should().Be("legacy_adserver|acct|campaign|BK-1|2026-09-01");
    }

    [Fact]
    public void Create_EscapesSeparatorsFoundInComponentValues()
    {
        var key = IdempotencyKey.Create(
            "partner",
            "acct|weird",
            "campaign",
            "c1",
            new DateOnly(2026, 9, 1),
            new Dictionary<string, string> { ["label"] = "a:b" });

        key.Should().Be("partner|acct%7Cweird|campaign|c1|2026-09-01|label:a%3Ab");
    }

    [Fact]
    public void Create_WithBlankRequiredComponent_Throws()
    {
        var act = () => IdempotencyKey.Create("tiktok_ads", "  ", "ad", "1", new DateOnly(2026, 9, 1));

        act.Should().Throw<ArgumentException>();
    }
}
