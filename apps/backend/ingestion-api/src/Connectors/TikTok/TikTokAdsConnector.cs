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

namespace IngestionApi.Connectors.TikTok;

public sealed class TikTokAdsConnector : ISourceConnector
{
    private const string EntityLevel = "ad";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly HttpClient _httpClient;
    private readonly TikTokOptions _options;
    private readonly IConnectorSecretResolver _secretResolver;
    private readonly IRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly Logger _logger = new("TikTokAdsConnector");

    public TikTokAdsConnector(
        HttpClient httpClient,
        TikTokOptions options,
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

    public string SourceKey => TikTokApiContract.SourceKey;

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

        var dimensions = TikTokApiContract.ResolveDimensions(request.Breakdowns);
        var pageSize = Math.Clamp(_options.PageSize, 1, TikTokApiContract.MaxPageSize);
        var page = 1;
        var totalPages = 1;
        var rowCount = 0;

        while (page <= totalPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _rateLimiter.AcquireAsync(SourceKey, cancellationToken).ConfigureAwait(false);

            var envelope = await SendReportRequestAsync(request, dimensions, accessToken, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

            if (envelope.Code != TikTokApiContract.SuccessCode)
            {
                throw new TikTokApiException(envelope.Code, envelope.Message, envelope.RequestId);
            }

            var data = envelope.Data;
            if (data is null)
            {
                yield break;
            }

            totalPages = data.PageInfo?.TotalPage ?? page;

            foreach (var row in data.List)
            {
                var record = BuildRecord(request, row, dimensions);
                if (record is not null)
                {
                    rowCount++;
                    yield return record;
                }
            }

            page++;
        }

        _logger.Info(
            "TikTok extraction completed",
            new Dictionary<string, object?>
            {
                ["runId"] = request.RunId,
                ["accountId"] = request.AccountId,
                ["rowCount"] = rowCount,
                ["pages"] = totalPages,
            });
    }

    private async Task<TikTokReportEnvelope> SendReportRequestAsync(
        ExtractionRequest request,
        IReadOnlyList<string> dimensions,
        string accessToken,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var uri = BuildRequestUri(request, dimensions, page, pageSize);
        var delay = _options.InitialRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Add(TikTokApiContract.AccessTokenHeader, accessToken);

            using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

            if (IsRetryable(response.StatusCode) && attempt <= _options.MaxRetries)
            {
                _logger.Warn(
                    "TikTok request throttled or unavailable, backing off",
                    new Dictionary<string, object?>
                    {
                        ["accountId"] = request.AccountId,
                        ["status"] = (int)response.StatusCode,
                        ["attempt"] = attempt,
                    });

                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
                delay += delay;
                continue;
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<TikTokReportEnvelope>(payload);

            return envelope ?? throw new TikTokApiException(-1, "TikTok returned an unreadable response body", null);
        }
    }

    private Uri BuildRequestUri(ExtractionRequest request, IReadOnlyList<string> dimensions, int page, int pageSize)
    {
        var query = new List<string>
        {
            $"advertiser_id={Uri.EscapeDataString(request.AccountId)}",
            $"report_type={TikTokApiContract.ReportType}",
            $"data_level={TikTokApiContract.DataLevel}",
            $"service_type={TikTokApiContract.ServiceType}",
            $"dimensions={Uri.EscapeDataString(JsonSerializer.Serialize(dimensions))}",
            $"metrics={Uri.EscapeDataString(JsonSerializer.Serialize(TikTokApiContract.Metrics))}",
            $"start_date={request.Window.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture)}",
            $"end_date={request.Window.EndDate.ToString(DateFormat, CultureInfo.InvariantCulture)}",
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"page_size={pageSize.ToString(CultureInfo.InvariantCulture)}",
        };

        return new Uri(_options.BaseAddress, $"{TikTokApiContract.ReportPath}?{string.Join('&', query)}");
    }

    private SourceRecord? BuildRecord(ExtractionRequest request, TikTokReportRow row, IReadOnlyList<string> dimensions)
    {
        if (row.Dimensions is null)
        {
            return null;
        }

        var adId = ReadDimension(row.Dimensions, TikTokApiContract.DimensionAdId);
        var statDate = ReadDimension(row.Dimensions, TikTokApiContract.DimensionStatTimeDay);

        if (string.IsNullOrWhiteSpace(adId) || !TryParseStatDate(statDate, out var metricDate))
        {
            _logger.Warn(
                "TikTok row skipped: natural key incomplete",
                new Dictionary<string, object?>
                {
                    ["accountId"] = request.AccountId,
                    ["adId"] = adId,
                    ["statTimeDay"] = statDate,
                });
            return null;
        }

        var breakdowns = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dimension in dimensions)
        {
            if (dimension is TikTokApiContract.DimensionAdId or TikTokApiContract.DimensionStatTimeDay)
            {
                continue;
            }

            var value = ReadDimension(row.Dimensions, dimension);
            if (!string.IsNullOrWhiteSpace(value))
            {
                breakdowns[dimension] = value;
            }
        }

        var payload = new JsonObject
        {
            ["dimensions"] = row.Dimensions.DeepClone(),
            ["metrics"] = row.Metrics?.DeepClone() ?? new JsonObject(),
        };

        return new SourceRecord
        {
            IdempotencyKey = IdempotencyKey.Create(
                SourceKey,
                request.AccountId,
                EntityLevel,
                adId,
                metricDate,
                breakdowns),
            PayloadFormat = PayloadFormat.Json,
            Payload = payload,
            ExtractedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            SourceWatermarkUtc = metricDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        };
    }

    private static bool TryParseStatDate(string? value, out DateOnly metricDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            metricDate = default;
            return false;
        }

        var datePart = value.Length > 10 ? value[..10] : value;
        return DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out metricDate);
    }

    private static string? ReadDimension(JsonObject dimensions, string name) =>
        dimensions.TryGetPropertyValue(name, out var node) ? node?.GetValue<string>() : null;

    private static bool IsRetryable(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string BuildSecretName(string accountId) => $"TIKTOK_ACCESS_TOKEN_{accountId}";
}
