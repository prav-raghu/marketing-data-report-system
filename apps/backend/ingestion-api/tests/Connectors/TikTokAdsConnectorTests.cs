using System.Net;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Connectors.TikTok;
using IngestionApi.Tests.Fixtures;
using Xunit;

namespace IngestionApi.Tests.Connectors;

public sealed class TikTokAdsConnectorTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 2, 1, 14, 9, TimeSpan.Zero);

    [Fact]
    public async Task ExtractAsync_MapsRowsToRecordsWithADeterministicKey()
    {
        var handler = new StubHttpMessageHandler().EnqueueJson(Page(1, 1, Row("178004", "2026-09-01")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
        records[0].IdempotencyKey.Should().Be("tiktok_ads|act_884213|ad|178004|2026-09-01");
        records[0].PayloadFormat.Should().Be(PayloadFormat.Json);
        records[0].ExtractedAtUtc.Should().Be(FixedUtcNow.UtcDateTime);
        records[0].SourceWatermarkUtc.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExtractAsync_IncludesBreakdownsInTheKeyWhenRequested()
    {
        var row = """
        {"dimensions":{"ad_id":"178004","stat_time_day":"2026-09-01 00:00:00","country_code":"ZA","platform":"ANDROID"},
         "metrics":{"spend":"41.55","impressions":"900"}}
        """;
        var handler = new StubHttpMessageHandler().EnqueueJson(Page(1, 1, row));
        var (connector, _, _) = Build(handler);

        var request = Request() with { Breakdowns = [BreakdownName.Geo, BreakdownName.Device] };
        var records = await Collect(connector, request);

        records[0].IdempotencyKey.Should()
            .Be("tiktok_ads|act_884213|ad|178004|2026-09-01|country_code:ZA|platform:ANDROID");
    }

    [Fact]
    public async Task ExtractAsync_FollowsPaginationToTheLastPage()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson(Page(1, 3, Row("a1", "2026-09-01")))
            .EnqueueJson(Page(2, 3, Row("a2", "2026-09-01")))
            .EnqueueJson(Page(3, 3, Row("a3", "2026-09-01")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(3);
        handler.Requests.Should().HaveCount(3);
        handler.Requests[0].Query.Should().Contain("page=1");
        handler.Requests[2].Query.Should().Contain("page=3");
    }

    [Fact]
    public async Task ExtractAsync_SendsTheResolvedAccessTokenAndWindow()
    {
        var handler = new StubHttpMessageHandler().EnqueueJson(Page(1, 1, Row("a1", "2026-09-01")));
        var (connector, secrets, _) = Build(handler);

        await Collect(connector, Request());

        secrets.RequestedSecretNames.Should().ContainSingle().Which.Should().Be("TIKTOK_ACCESS_TOKEN_act_884213");
        handler.AccessTokens.Should().ContainSingle().Which.Should().Be("token-value");
        handler.Requests[0].Query.Should().Contain("start_date=2026-08-26").And.Contain("end_date=2026-09-01");
        handler.Requests[0].Query.Should().Contain("advertiser_id=act_884213");
    }

    [Fact]
    public async Task ExtractAsync_TakesARateLimitPermitPerPage()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson(Page(1, 2, Row("a1", "2026-09-01")))
            .EnqueueJson(Page(2, 2, Row("a2", "2026-09-01")));
        var (connector, _, limiter) = Build(handler);

        await Collect(connector, Request());

        limiter.AcquiredPartitions.Should().Equal("tiktok_ads", "tiktok_ads");
    }

    [Fact]
    public async Task ExtractAsync_WhenTikTokReturnsANonZeroCode_Throws()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson("""{"code":40001,"message":"Invalid advertiser_id","request_id":"req-1"}""");
        var (connector, _, _) = Build(handler);

        var act = async () => await Collect(connector, Request());

        var exception = await act.Should().ThrowAsync<TikTokApiException>();
        exception.Which.Code.Should().Be(40001);
        exception.Which.RequestId.Should().Be("req-1");
    }

    [Fact]
    public async Task ExtractAsync_RetriesOnThrottlingThenSucceeds()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueStatus(HttpStatusCode.TooManyRequests)
            .EnqueueJson(Page(1, 1, Row("a1", "2026-09-01")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExtractAsync_SkipsRowsMissingTheNaturalKeyRatherThanFailingTheBatch()
    {
        var incomplete = """{"dimensions":{"stat_time_day":"2026-09-01 00:00:00"},"metrics":{"spend":"1"}}""";
        var handler = new StubHttpMessageHandler().EnqueueJson(Page(1, 1, incomplete, Row("a2", "2026-09-01")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
        records[0].IdempotencyKey.Should().Contain("|a2|");
    }

    [Fact]
    public async Task ExtractAsync_PreservesBothDimensionsAndMetricsInThePayload()
    {
        var handler = new StubHttpMessageHandler().EnqueueJson(Page(1, 1, Row("a1", "2026-09-01")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());
        var payload = records[0].Payload;

        payload["dimensions"]!["ad_id"]!.GetValue<string>().Should().Be("a1");
        payload["metrics"]!["spend"]!.GetValue<string>().Should().Be("41.55");
    }

    private static async Task<List<SourceRecord>> Collect(ISourceConnector connector, ExtractionRequest request)
    {
        var records = new List<SourceRecord>();
        await foreach (var record in connector.ExtractAsync(request, CancellationToken.None))
        {
            records.Add(record);
        }

        return records;
    }

    private static (TikTokAdsConnector Connector, StaticSecretResolver Secrets, PassThroughRateLimiter Limiter) Build(
        StubHttpMessageHandler handler)
    {
        var secrets = new StaticSecretResolver("token-value");
        var limiter = new PassThroughRateLimiter();
        var httpClient = new HttpClient(handler);
        var options = new TikTokOptions
        {
            BaseAddress = new Uri("https://business-api.tiktok.test/"),
            InitialRetryDelay = TimeSpan.Zero,
        };

        return (
            new TikTokAdsConnector(httpClient, options, secrets, limiter, new FixedTimeProvider(FixedUtcNow)),
            secrets,
            limiter);
    }

    private static ExtractionRequest Request() => new()
    {
        RunId = "01J8ZQ",
        SourceSystem = TikTokApiContract.SourceKey,
        SourceEntity = TikTokApiContract.SourceEntity,
        ContractVersion = TikTokApiContract.ContractVersion,
        AccountId = "act_884213",
        Window = ExtractionWindow.Restatement(new DateOnly(2026, 9, 1), 7),
        Tier = AccountTier.Tier1,
    };

    private static string Row(string adId, string date) =>
        "{\"dimensions\":{\"ad_id\":\"" + adId + "\",\"stat_time_day\":\"" + date
        + " 00:00:00\"},\"metrics\":{\"spend\":\"41.55\",\"impressions\":\"900\"}}";

    private static string Page(int page, int totalPage, params string[] rows) =>
        "{\"code\":0,\"message\":\"OK\",\"request_id\":\"req-" + page
        + "\",\"data\":{\"page_info\":{\"page\":" + page
        + ",\"page_size\":1000,\"total_number\":" + rows.Length
        + ",\"total_page\":" + totalPage
        + "},\"list\":[" + string.Join(",", rows) + "]}}";
}
