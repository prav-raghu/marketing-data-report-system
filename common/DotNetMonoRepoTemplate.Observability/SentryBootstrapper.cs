using Sentry;

namespace DotNetMonoRepoTemplate.Observability;

public static class SentryBootstrapper
{
    public static IDisposable? Init()
    {
        var config = SentryConfigResolver.Resolve();
        if (!config.Enabled)
        {
            return null;
        }

        return SentrySdk.Init(options =>
        {
            options.Dsn = config.Dsn;
            options.Environment = config.Environment;
            options.Release = config.Release;
            options.TracesSampleRate = config.TracesSampleRate;
        });
    }
}

public static class SentryCapture
{
    public static void CaptureException(Exception exception) => SentrySdk.CaptureException(exception);
}
