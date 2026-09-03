using System.Globalization;

namespace DotNetMonoRepoTemplate.Observability;

public sealed record SentryConfig
{
    public required string Dsn { get; init; }
    public required string Environment { get; init; }
    public string? Release { get; init; }
    public required double TracesSampleRate { get; init; }
    public required bool Enabled { get; init; }
}

public static class SentryConfigResolver
{
    private const double DefaultTracesSampleRate = 0.1;
    private const string DefaultEnvironment = "development";

    public static SentryConfig Resolve()
    {
        var dsn = System.Environment.GetEnvironmentVariable("SENTRY_DSN") ?? string.Empty;
        var environment = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? DefaultEnvironment;
        var release = System.Environment.GetEnvironmentVariable("SENTRY_RELEASE");
        var tracesSampleRate = ParseSampleRate(System.Environment.GetEnvironmentVariable("SENTRY_TRACES_SAMPLE_RATE"));

        return new SentryConfig
        {
            Dsn = dsn,
            Environment = environment,
            Release = release,
            TracesSampleRate = tracesSampleRate,
            Enabled = dsn != string.Empty,
        };
    }

    private static double ParseSampleRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultTracesSampleRate;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
            ? parsed
            : DefaultTracesSampleRate;
    }
}
