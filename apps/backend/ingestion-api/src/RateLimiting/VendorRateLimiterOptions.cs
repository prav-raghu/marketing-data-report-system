namespace IngestionApi.RateLimiting;

public sealed record VendorRateLimiterOptions
{
    public int PermitsPerWindow { get; init; } = 500;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan AcquireTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
