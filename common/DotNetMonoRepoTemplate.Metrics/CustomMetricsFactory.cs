using Prometheus;

namespace DotNetMonoRepoTemplate.Metrics;

public sealed class CustomMetricsFactory
{
    internal static readonly double[] DefaultBuckets = { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 };

    private readonly string _prefix;

    public CustomMetricsFactory(string prefix = "") => _prefix = prefix;

    public Counter CreateCounter(string name, string help, string[]? labelNames = null) =>
        Prometheus.Metrics.CreateCounter(
            $"{_prefix}{name}",
            help,
            new CounterConfiguration { LabelNames = labelNames ?? Array.Empty<string>() });

    public Gauge CreateGauge(string name, string help, string[]? labelNames = null) =>
        Prometheus.Metrics.CreateGauge(
            $"{_prefix}{name}",
            help,
            new GaugeConfiguration { LabelNames = labelNames ?? Array.Empty<string>() });

    public Histogram CreateHistogram(string name, string help, string[]? labelNames = null, double[]? buckets = null) =>
        Prometheus.Metrics.CreateHistogram(
            $"{_prefix}{name}",
            help,
            new HistogramConfiguration
            {
                LabelNames = labelNames ?? Array.Empty<string>(),
                Buckets = buckets ?? DefaultBuckets,
            });

    public DatabaseMetrics CreateDatabaseMetrics() => new(_prefix);

    public CacheMetrics CreateCacheMetrics() => new(_prefix);
}
