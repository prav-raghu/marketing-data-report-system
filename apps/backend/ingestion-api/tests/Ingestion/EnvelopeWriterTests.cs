using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Ingestion.Envelope;
using DotNetMonoRepoTemplate.Ingestion.Writing;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Tests.Fixtures;
using Xunit;

namespace IngestionApi.Tests.Ingestion;

public sealed class EnvelopeWriterTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 2, 1, 14, 9, TimeSpan.Zero);

    [Fact]
    public async Task WriteAsync_WrapsEachRecordInAnEnvelope()
    {
        var rawZone = new InMemoryRawZoneWriter();
        var writer = new EnvelopeWriter(rawZone, new EnvelopeWriterOptions(), new FixedTimeProvider(FixedUtcNow));

        var result = await writer.WriteAsync(CreateRequest(), Records(1), CancellationToken.None);

        result.RecordCount.Should().Be(1);
        result.PartCount.Should().Be(1);

        var envelope = EnvelopeSerializer.Deserialize(rawZone.ReadLines(result.Paths[0])[0]);

        envelope.Should().NotBeNull();
        envelope!.EnvelopeVersion.Should().Be(EnvelopeConstants.CurrentVersion);
        envelope.SourceSystem.Should().Be("tiktok_ads");
        envelope.SourceEntity.Should().Be("ad_insights_daily");
        envelope.ContractVersion.Should().Be("2026-06");
        envelope.RunId.Should().Be("01J8ZQ");
        envelope.PayloadFormat.Should().Be(PayloadFormat.Json);
        envelope.PayloadHash.Should().StartWith("sha256:");
        envelope.IngestedAtUtc.Should().Be(FixedUtcNow.UtcDateTime);
        envelope.Payload["ad_id"]!.GetValue<string>().Should().Be("ad-0");
    }

    [Fact]
    public async Task WriteAsync_SplitsIntoPartsOnTheRecordLimit()
    {
        var rawZone = new InMemoryRawZoneWriter();
        var options = new EnvelopeWriterOptions { MaxRecordsPerPart = 2 };
        var writer = new EnvelopeWriter(rawZone, options, new FixedTimeProvider(FixedUtcNow));

        var result = await writer.WriteAsync(CreateRequest(), Records(5), CancellationToken.None);

        result.RecordCount.Should().Be(5);
        result.PartCount.Should().Be(3);
        result.Paths.Should().Equal(
            "source=tiktok_ads/entity=ad_insights_daily/ingest_date=2026-09-02/run_id=01J8ZQ/part-00000.json.gz",
            "source=tiktok_ads/entity=ad_insights_daily/ingest_date=2026-09-02/run_id=01J8ZQ/part-00001.json.gz",
            "source=tiktok_ads/entity=ad_insights_daily/ingest_date=2026-09-02/run_id=01J8ZQ/part-00002.json.gz");

        rawZone.ReadLines(result.Paths[0]).Should().HaveCount(2);
        rawZone.ReadLines(result.Paths[2]).Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteAsync_WritesOneJsonDocumentPerLine()
    {
        var rawZone = new InMemoryRawZoneWriter();
        var writer = new EnvelopeWriter(rawZone, new EnvelopeWriterOptions(), new FixedTimeProvider(FixedUtcNow));

        var result = await writer.WriteAsync(CreateRequest(), Records(3), CancellationToken.None);

        var lines = rawZone.ReadLines(result.Paths[0]);

        lines.Should().HaveCount(3);
        lines.Should().OnlyContain(line => EnvelopeSerializer.Deserialize(line) != null);
    }

    [Fact]
    public async Task WriteAsync_WithNoRecords_WritesNothing()
    {
        var rawZone = new InMemoryRawZoneWriter();
        var writer = new EnvelopeWriter(rawZone, new EnvelopeWriterOptions(), new FixedTimeProvider(FixedUtcNow));

        var result = await writer.WriteAsync(CreateRequest(), Records(0), CancellationToken.None);

        result.RecordCount.Should().Be(0);
        result.PartCount.Should().Be(0);
        result.CompressedBytes.Should().Be(0);
        rawZone.Written.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAsync_PreservesTheOriginalArtifactPointerForConvertedPayloads()
    {
        var rawZone = new InMemoryRawZoneWriter();
        var writer = new EnvelopeWriter(rawZone, new EnvelopeWriterOptions(), new FixedTimeProvider(FixedUtcNow));

        var records = ToAsyncEnumerable([
            new SourceRecord
            {
                IdempotencyKey = "legacy|acct|campaign|BK-1|2026-09-01",
                PayloadFormat = PayloadFormat.Xml,
                Payload = JsonNode.Parse("""{"CampaignName":"Spring Sale"}""")!,
                ExtractedAtUtc = FixedUtcNow.UtcDateTime,
                RawArtifactPath = "source=legacy/entity=booking/ingest_date=2026-09-02/run_id=r/original/artifact-00000.xml",
            },
        ]);

        var result = await writer.WriteAsync(CreateRequest(), records, CancellationToken.None);
        var envelope = EnvelopeSerializer.Deserialize(rawZone.ReadLines(result.Paths[0])[0]);

        envelope!.PayloadFormat.Should().Be(PayloadFormat.Xml);
        envelope.RawArtifactPath.Should().NotBeNull();
    }

    private static ExtractionRequest CreateRequest() => new()
    {
        RunId = "01J8ZQ",
        SourceSystem = "tiktok_ads",
        SourceEntity = "ad_insights_daily",
        ContractVersion = "2026-06",
        AccountId = "act_884213",
        Window = ExtractionWindow.ForSingleDay(new DateOnly(2026, 9, 1)),
        Tier = AccountTier.Tier1,
    };

    private static async IAsyncEnumerable<SourceRecord> Records(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return new SourceRecord
            {
                IdempotencyKey = $"tiktok_ads|act_884213|ad|ad-{i}|2026-09-01",
                PayloadFormat = PayloadFormat.Json,
                Payload = JsonNode.Parse($$"""{"ad_id":"ad-{{i}}","spend":"41.55"}""")!,
                ExtractedAtUtc = FixedUtcNow.UtcDateTime,
            };
        }
    }

    private static async IAsyncEnumerable<SourceRecord> ToAsyncEnumerable(IReadOnlyList<SourceRecord> records)
    {
        foreach (var record in records)
        {
            await Task.Yield();
            yield return record;
        }
    }
}
