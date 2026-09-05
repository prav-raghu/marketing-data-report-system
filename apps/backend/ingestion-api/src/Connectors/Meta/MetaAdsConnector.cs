using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Ingestion.Keys;
using DotNetMonoRepoTemplate.Logging;
using DotNetMonoRepoTemplate.Types;
using IngestionApi.RateLimiting;

namespace IngestionApi.Connectors.Meta;

public sealed class MetaAdsConnector : ISourceConnector
{
    private const string EntityLevel = "ad";
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly Dictionary<string, string> EmptyParameters = new(StringComparer.Ordinal);

    private readonly HttpClient _httpClient;
    private readonly MetaOptions _options;
    private readonly IConnectorSecretResolver _secretResolver;
    private readonly IRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly Logger _logger = new("MetaAdsConnector");

    public MetaAdsConnector(
        HttpClient httpClient,
        MetaOptions options,
        IConnectorSecretResolver secretResolver,
        IRateLimiter rateLimiter,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _options = options;
        _secretResolver = secretResolver;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
    }

    public string SourceKey => MetaApiContract.SourceKey;

    public SourceCapabilities Capabilities { get; } = new()
    {
        NativeFormat = PayloadFormat.Json,
        SupportsIncremental = true,
        SupportsBreakdowns = true,
        SupportsAttributionWindows = true,
        MaxRestatementDays = 28,
    };

    public async IAsyncEnumerable<SourceRecord> ExtractAsync(
        ExtractionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessToken = await _secretResolver
            .ResolveAsync(BuildSecretName(request.AccountId), cancellationToken)
            .ConfigureAwait(false);

        var breakdowns = MetaApiContract.ResolveBreakdowns(request.Breakdowns);
        var reportRunId = await SubmitAsync(request, breakdowns, accessToken, cancellationToken).ConfigureAwait(false);
        await AwaitCompletionAsync(reportRunId, accessToken, cancellationToken).ConfigureAwait(false);

        var after = (string?)null;
        var rowCount = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.AcquireAsync(SourceKey, cancellationToken).ConfigureAwait(false);

            var page = await ReadResultPageAsync(reportRunId, accessToken, after, cancellationToken).ConfigureAwait(false);

            foreach (var row in page.Data)
            {
                var record = BuildRecord(request, row, breakdowns);
                if (record is not null)
                {
                    rowCount++;
                    yield return record;
                }
            }

            after = page.Data.Count > 0 ? page.Paging?.Cursors?.After : null;
        }
        while (!string.IsNullOrEmpty(after));

        _logger.Info(
            "Meta extraction completed",
            new Dictionary<string, object?>
            {
                ["runId"] = request.RunId,
                ["accountId"] = request.AccountId,
                ["reportRunId"] = reportRunId,
                ["rowCount"] = rowCount,
            });
    }

    private async Task<string> SubmitAsync(
        ExtractionRequest request,
        IReadOnlyList<string> breakdowns,
        string accessToken,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.AcquireAsync(SourceKey, cancellationToken).ConfigureAwait(false);

        var timeRange = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["since"] = request.Window.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture),
            ["until"] = request.Window.EndDate.ToString(DateFormat, CultureInfo.InvariantCulture),
        });

        var form = new Dictionary<string, string>
        {
            [MetaApiContract.AccessTokenParameter] = accessToken,
            ["level"] = MetaApiContract.Level,
            ["time_increment"] = MetaApiContract.TimeIncrementDaily,
            ["time_range"] = timeRange,
            ["fields"] = string.Join(',', MetaApiContract.Fields),
            ["breakdowns"] = string.Join(',', breakdowns),
        };

        var uri = new Uri(_options.BaseAddress,
            $"{_options.ApiVersion}/{Uri.EscapeDataString(request.AccountId)}/{MetaApiContract.InsightsPathSegment}");

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(form) },
            cancellationToken).ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var submission = JsonSerializer.Deserialize<MetaAsyncJobSubmission>(payload);

        if (string.IsNullOrWhiteSpace(submission?.ReportRunId))
        {
            throw new MetaApiException("Meta did not return a report_run_id for the insights job");
        }

        return submission.ReportRunId;
    }

    private async Task AwaitCompletionAsync(string reportRunId, string accessToken, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxPollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.AcquireAsync(SourceKey, cancellationToken).ConfigureAwait(false);

            var uri = BuildUri($"{_options.ApiVersion}/{reportRunId}", accessToken, EmptyParameters);
            using var response = await SendWithRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, uri),
                cancellationToken).ConfigureAwait(false);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var status = JsonSerializer.Deserialize<MetaAsyncJobStatus>(payload);

            if (MetaApiContract.IsComplete(status?.AsyncStatus))
            {
                return;
            }

            if (MetaApiContract.IsTerminalFailure(status?.AsyncStatus))
            {
                throw new MetaApiException(
                    $"Meta insights job ended with status '{status?.AsyncStatus}'",
                    reportRunId);
            }

            await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        throw new MetaApiException(
            $"Meta insights job did not complete within {_options.MaxPollAttempts} polls",
            reportRunId);
    }

    private async Task<MetaInsightsPage> ReadResultPageAsync(
        string reportRunId,
        string accessToken,
        string? after,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["limit"] = _options.PageSize.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrEmpty(after))
        {
            parameters["after"] = after;
        }

        var uri = BuildUri(
            $"{_options.ApiVersion}/{reportRunId}/{MetaApiContract.InsightsPathSegment}",
            accessToken,
            parameters);

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            cancellationToken).ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<MetaInsightsPage>(payload)
            ?? throw new MetaApiException("Meta returned an unreadable insights page", reportRunId);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var delay = _options.InitialRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            using var message = requestFactory();
            var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

            if (IsRetryable(response.StatusCode) && attempt <= _options.MaxRetries)
            {
                response.Dispose();
                _logger.Warn(
                    "Meta request throttled or unavailable, backing off",
                    new Dictionary<string, object?> { ["attempt"] = attempt });

                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
                delay += delay;
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }
    }

    private Uri BuildUri(string path, string accessToken, IReadOnlyDictionary<string, string> parameters)
    {
        var query = new List<string>
        {
            $"{MetaApiContract.AccessTokenParameter}={Uri.EscapeDataString(accessToken)}",
        };

        foreach (var parameter in parameters)
        {
            query.Add($"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}");
        }

        return new Uri(_options.BaseAddress, $"{path}?{string.Join('&', query)}");
    }

    private SourceRecord? BuildRecord(ExtractionRequest request, JsonObject row, IReadOnlyList<string> breakdowns)
    {
        var adId = ReadString(row, MetaApiContract.FieldAdId);
        var dateStart = ReadString(row, MetaApiContract.FieldDateStart);

        if (string.IsNullOrWhiteSpace(adId) || !TryParseDate(dateStart, out var metricDate))
        {
            _logger.Warn(
                "Meta row skipped: natural key incomplete",
                new Dictionary<string, object?>
                {
                    ["accountId"] = request.AccountId,
                    ["adId"] = adId,
                    ["dateStart"] = dateStart,
                });
            return null;
        }

        var breakdownValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var breakdown in breakdowns)
        {
            var value = ReadString(row, breakdown);
            if (!string.IsNullOrWhiteSpace(value))
            {
                breakdownValues[breakdown] = value;
            }
        }

        return new SourceRecord
        {
            IdempotencyKey = IdempotencyKey.Create(
                SourceKey,
                request.AccountId,
                EntityLevel,
                adId,
                metricDate,
                breakdownValues),
            PayloadFormat = PayloadFormat.Json,
            Payload = row.DeepClone(),
            ExtractedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            SourceWatermarkUtc = metricDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        };
    }

    private static bool TryParseDate(string? value, out DateOnly metricDate) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out metricDate);

    private static string? ReadString(JsonObject row, string name) =>
        row.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string BuildSecretName(string accountId) => $"META_ACCESS_TOKEN_{accountId}";
}
