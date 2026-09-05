using System.Net;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Types;
using FluentAssertions;
using IngestionApi.Connectors.Meta;
using IngestionApi.Tests.Fixtures;
using Xunit;

namespace IngestionApi.Tests.Connectors;

public sealed class MetaAdsConnectorTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 2, 1, 14, 9, TimeSpan.Zero);

    [Fact]
    public async Task ExtractAsync_RunsSubmitPollDownloadInOrder()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson(Submission("run-1"))
            .EnqueueJson(Status(MetaApiContract.StatusRunning))
            .EnqueueJson(Status(MetaApiContract.StatusCompleted))
            .EnqueueJson(Page(null, Row("a1", "2026-09-01", "facebook")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
        handler.Requests.Should().HaveCount(4);
        handler.Requests[0].AbsolutePath.Should().EndWith("/v21.0/act_884213/insights");
        handler.Requests[1].AbsolutePath.Should().EndWith("/v21.0/run-1");
        handler.Requests[3].AbsolutePath.Should().EndWith("/v21.0/run-1/insights");
    }

    [Fact]
    public async Task ExtractAsync_AlwaysRequestsPublisherPlatformSoFacebookAndInstagramAreSeparable()
    {
        var handler = Completed().EnqueueJson(Page(null, Row("a1", "2026-09-01", "instagram")));
        var (connector, _, _) = Build(handler);

        await Collect(connector, Request());

        handler.FormBodies[0].Should().Contain("publisher_platform");
    }

    [Fact]
    public async Task ExtractAsync_PutsPublisherPlatformIntoTheIdempotencyKey()
    {
        var handler = Completed().EnqueueJson(Page(null, Row("a1", "2026-09-01", "instagram")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records[0].IdempotencyKey.Should()
            .Be("meta_ads|act_884213|ad|a1|2026-09-01|publisher_platform:instagram");
    }

    [Fact]
    public async Task ExtractAsync_FollowsCursorPaginationUntilExhausted()
    {
        var handler = Completed()
            .EnqueueJson(Page("cursor-1", Row("a1", "2026-09-01", "facebook")))
            .EnqueueJson(Page(null, Row("a2", "2026-09-01", "facebook")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(2);
        handler.Requests[^1].Query.Should().Contain("after=cursor-1");
    }

    [Fact]
    public async Task ExtractAsync_StopsPagingWhenAPageComesBackEmpty()
    {
        var handler = Completed().EnqueueJson(Page("cursor-1"));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().BeEmpty();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheJobFails_Throws()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson(Submission("run-9"))
            .EnqueueJson(Status(MetaApiContract.StatusFailed));
        var (connector, _, _) = Build(handler);

        var act = async () => await Collect(connector, Request());

        var exception = await act.Should().ThrowAsync<MetaApiException>();
        exception.Which.ReportRunId.Should().Be("run-9");
        exception.Which.Message.Should().Contain(MetaApiContract.StatusFailed);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheJobIsSkipped_Throws()
    {
        var handler = new StubHttpMessageHandler()
            .EnqueueJson(Submission("run-8"))
            .EnqueueJson(Status(MetaApiContract.StatusSkipped));
        var (connector, _, _) = Build(handler);

        var act = async () => await Collect(connector, Request());

        await act.Should().ThrowAsync<MetaApiException>();
    }

    [Fact]
    public async Task ExtractAsync_WhenPollingExceedsTheBudget_Throws()
    {
        var handler = new StubHttpMessageHandler().EnqueueJson(Submission("run-7"));
        for (var i = 0; i < 3; i++)
        {
            handler.EnqueueJson(Status(MetaApiContract.StatusRunning));
        }

        var (connector, _, _) = Build(handler, options => options with { MaxPollAttempts = 3 });

        var act = async () => await Collect(connector, Request());

        var exception = await act.Should().ThrowAsync<MetaApiException>();
        exception.Which.Message.Should().Contain("did not complete");
    }

    [Fact]
    public async Task ExtractAsync_WhenNoReportRunIdComesBack_Throws()
    {
        var handler = new StubHttpMessageHandler().EnqueueJson("{}");
        var (connector, _, _) = Build(handler);

        var act = async () => await Collect(connector, Request());

        await act.Should().ThrowAsync<MetaApiException>()
            .WithMessage("*did not return a report_run_id*");
    }

    [Fact]
    public async Task ExtractAsync_PreservesTheActionsArrayVerbatim()
    {
        var row = """
        {"ad_id":"a1","date_start":"2026-09-01","publisher_platform":"facebook","spend":"41.55",
         "actions":[{"action_type":"purchase","value":"3"},{"action_type":"add_to_cart","value":"11"}]}
        """;
        var handler = Completed().EnqueueJson(Page(null, row));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());
        var actions = records[0].Payload["actions"]!.AsArray();

        actions.Should().HaveCount(2);
        actions[0]!["action_type"]!.GetValue<string>().Should().Be("purchase");
        actions[1]!["action_type"]!.GetValue<string>().Should().Be("add_to_cart");
    }

    [Fact]
    public async Task ExtractAsync_RetriesOnThrottlingDuringDownload()
    {
        var handler = Completed()
            .EnqueueStatus(HttpStatusCode.TooManyRequests)
            .EnqueueJson(Page(null, Row("a1", "2026-09-01", "facebook")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtractAsync_SkipsRowsMissingTheNaturalKey()
    {
        var incomplete = """{"date_start":"2026-09-01","publisher_platform":"facebook"}""";
        var handler = Completed().EnqueueJson(Page(null, incomplete, Row("a2", "2026-09-01", "facebook")));
        var (connector, _, _) = Build(handler);

        var records = await Collect(connector, Request());

        records.Should().HaveCount(1);
        records[0].IdempotencyKey.Should().Contain("|a2|");
    }

    [Fact]
    public async Task ExtractAsync_TakesARateLimitPermitForSubmitPollAndEachPage()
    {
        var handler = Completed().EnqueueJson(Page(null, Row("a1", "2026-09-01", "facebook")));
        var (connector, _, limiter) = Build(handler);

        await Collect(connector, Request());

        limiter.AcquiredPartitions.Should().OnlyContain(key => key == "meta_ads");
        limiter.AcquiredPartitions.Should().HaveCount(3);
    }

    private static StubHttpMessageHandler Completed() =>
        new StubHttpMessageHandler()
            .EnqueueJson(Submission("run-1"))
            .EnqueueJson(Status(MetaApiContract.StatusCompleted));

    private static async Task<List<SourceRecord>> Collect(ISourceConnector connector, ExtractionRequest request)
    {
        var records = new List<SourceRecord>();
        await foreach (var record in connector.ExtractAsync(request, CancellationToken.None))
        {
            records.Add(record);
        }

        return records;
    }

    private static (MetaAdsConnector Connector, StaticSecretResolver Secrets, PassThroughRateLimiter Limiter) Build(
        StubHttpMessageHandler handler,
        Func<MetaOptions, MetaOptions>? configure = null)
    {
        var secrets = new StaticSecretResolver("token-value");
        var limiter = new PassThroughRateLimiter();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.facebook.test/") };
        var options = new MetaOptions
        {
            BaseAddress = new Uri("https://graph.facebook.test/"),
            PollInterval = TimeSpan.Zero,
            InitialRetryDelay = TimeSpan.Zero,
        };

        return (
            new MetaAdsConnector(httpClient, configure?.Invoke(options) ?? options, secrets, limiter, new FixedTimeProvider(FixedUtcNow)),
            secrets,
            limiter);
    }

    private static ExtractionRequest Request() => new()
    {
        RunId = "01J8ZQ",
        SourceSystem = MetaApiContract.SourceKey,
        SourceEntity = MetaApiContract.SourceEntity,
        ContractVersion = MetaApiContract.ContractVersion,
        AccountId = "act_884213",
        Window = ExtractionWindow.Restatement(new DateOnly(2026, 9, 1), 7),
        Tier = AccountTier.Tier1,
    };

    private static string Submission(string reportRunId) =>
        "{\"report_run_id\":\"" + reportRunId + "\"}";

    private static string Status(string status) =>
        "{\"id\":\"run-1\",\"async_status\":\"" + status + "\",\"async_percent_completion\":100}";

    private static string Row(string adId, string date, string platform) =>
        "{\"ad_id\":\"" + adId + "\",\"date_start\":\"" + date
        + "\",\"publisher_platform\":\"" + platform + "\",\"spend\":\"41.55\"}";

    private static string Page(string? after, params string[] rows)
    {
        var paging = after is null
            ? "{\"cursors\":{}}"
            : "{\"cursors\":{\"after\":\"" + after + "\"}}";
        return "{\"data\":[" + string.Join(",", rows) + "],\"paging\":" + paging + "}";
    }
}
