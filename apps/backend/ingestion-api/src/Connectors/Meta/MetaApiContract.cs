using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Connectors.Meta;

public static class MetaApiContract
{
    public const string SourceKey = "meta_ads";
    public const string SourceEntity = "ad_insights_daily";
    public const string ContractVersion = "2026-06";

    public const string AccessTokenParameter = "access_token";
    public const string InsightsPathSegment = "insights";

    public const string Level = "ad";
    public const string TimeIncrementDaily = "1";

    public const string StatusNotStarted = "Job Not Started";
    public const string StatusStarted = "Job Started";
    public const string StatusRunning = "Job Running";
    public const string StatusCompleted = "Job Completed";
    public const string StatusFailed = "Job Failed";
    public const string StatusSkipped = "Job Skipped";

    public const string FieldDateStart = "date_start";
    public const string FieldAdId = "ad_id";

    public const string BreakdownPublisherPlatform = "publisher_platform";

    public static readonly IReadOnlyList<string> Fields =
    [
        "date_start",
        "date_stop",
        "account_id",
        "campaign_id",
        "adset_id",
        "ad_id",
        "spend",
        "impressions",
        "clicks",
        "inline_link_clicks",
        "actions",
        "action_values",
        "reach",
        "frequency",
        "video_p25_watched_actions",
        "video_p50_watched_actions",
        "video_p75_watched_actions",
        "video_p100_watched_actions",
    ];

    private static readonly Dictionary<string, string> BreakdownDimensions = new(StringComparer.Ordinal)
    {
        [BreakdownName.Geo] = "country",
        [BreakdownName.Device] = "impression_device",
        [BreakdownName.Placement] = "platform_position",
        [BreakdownName.AgeGender] = "age",
    };

    public static IReadOnlyList<string> ResolveBreakdowns(IReadOnlyList<string> breakdowns)
    {
        var resolved = new List<string> { BreakdownPublisherPlatform };

        foreach (var breakdown in breakdowns)
        {
            if (BreakdownDimensions.TryGetValue(breakdown, out var dimension) && !resolved.Contains(dimension))
            {
                resolved.Add(dimension);
            }
        }

        return resolved;
    }

    public static bool IsTerminalFailure(string? status) =>
        status is StatusFailed or StatusSkipped;

    public static bool IsComplete(string? status) => status == StatusCompleted;
}
