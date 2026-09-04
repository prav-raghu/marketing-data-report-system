using DotNetMonoRepoTemplate.Ingestion.Lake;
using FluentAssertions;
using Xunit;

namespace IngestionApi.Tests.Ingestion;

public sealed class RawZonePathTests
{
    [Fact]
    public void ForPart_BuildsThePartitionedPath()
    {
        var path = RawZonePath.ForPart("tiktok_ads", "ad_insights_daily", new DateOnly(2026, 9, 2), "01J8ZQ", 0);

        path.Should().Be("source=tiktok_ads/entity=ad_insights_daily/ingest_date=2026-09-02/run_id=01J8ZQ/part-00000.json.gz");
    }

    [Fact]
    public void ForOriginalArtifact_KeepsTheSourceDocumentBesideTheParts()
    {
        var path = RawZonePath.ForOriginalArtifact(
            "legacy_adserver",
            "booking_report",
            new DateOnly(2026, 9, 2),
            "01J8ZR",
            3,
            ".xml");

        path.Should().Be("source=legacy_adserver/entity=booking_report/ingest_date=2026-09-02/run_id=01J8ZR/original/artifact-00003.xml");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with/slash")]
    [InlineData("..")]
    [InlineData("has space")]
    public void ForPart_RejectsUnsafeSegments(string sourceSystem)
    {
        var act = () => RawZonePath.ForPart(sourceSystem, "entity", new DateOnly(2026, 9, 2), "run", 0);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForPart_RejectsNegativeSequence()
    {
        var act = () => RawZonePath.ForPart("tiktok_ads", "entity", new DateOnly(2026, 9, 2), "run", -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
