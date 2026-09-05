using DotNetMonoRepoTemplate.Types;

namespace IngestionApi.Connectors.TikTok;

public static class TikTokApiContract
{
    public const string SourceKey = "tiktok_ads";
    public const string SourceEntity = "ad_insights_daily";
    public const string ContractVersion = "2026-06";

    public const string ReportPath = "open_api/v1.3/report/integrated/get/";
    public const string AccessTokenHeader = "Access-Token";

    public const string ReportType = "BASIC";
    public const string DataLevel = "AUCTION_AD";
    public const string ServiceType = "AUCTION";

    public const int SuccessCode = 0;
    public const int MaxPageSize = 1000;

    public const string DimensionAdId = "ad_id";
    public const string DimensionStatTimeDay = "stat_time_day";

    public static readonly IReadOnlyList<string> Metrics =
    [
        "spend",
        "impressions",
        "clicks",
        "conversion",
        "total_purchase_value",
        "video_watched_2s",
        "video_views_p25",
        "video_views_p50",
        "video_views_p75",
        "video_views_p100",
        "reach",
        "frequency",
    ];

    private static readonly Dictionary<string, string> BreakdownDimensions = new(StringComparer.Ordinal)
    {
        [BreakdownName.Geo] = "country_code",
        [BreakdownName.Device] = "platform",
        [BreakdownName.Placement] = "placement_type",
        [BreakdownName.AgeGender] = "age",
    };

    public static IReadOnlyList<string> ResolveDimensions(IReadOnlyList<string> breakdowns)
    {
        var dimensions = new List<string> { DimensionAdId, DimensionStatTimeDay };

        foreach (var breakdown in breakdowns)
        {
            if (BreakdownDimensions.TryGetValue(breakdown, out var dimension))
            {
                dimensions.Add(dimension);
            }
        }

        return dimensions;
    }

    public static IReadOnlyList<string> BreakdownDimensionNames =>
        BreakdownDimensions.Values.ToList();
}
