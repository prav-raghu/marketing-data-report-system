using Prometheus;

namespace DotNetMonoRepoTemplate.Metrics;

public sealed class CacheMetrics
{
    public Histogram OperationDuration { get; }
    public Counter OperationsTotal { get; }
    public Gauge HitRate { get; }
    public Gauge MissRate { get; }
    public Gauge KeysTotal { get; }
    public Gauge MemoryUsage { get; }

    private long _hits;
    private long _misses;

    public CacheMetrics(string prefix = "")
    {
        OperationDuration = Prometheus.Metrics.CreateHistogram(
            $"{prefix}cache_operation_duration_seconds",
            "Cache operation duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation" },
                Buckets = new[] { 0.0001, 0.0005, 0.001, 0.005, 0.01, 0.025, 0.05, 0.1 },
            });

        OperationsTotal = Prometheus.Metrics.CreateCounter(
            $"{prefix}cache_operations_total",
            "Total number of cache operations",
            new CounterConfiguration { LabelNames = new[] { "operation", "status" } });

        HitRate = Prometheus.Metrics.CreateGauge($"{prefix}cache_hit_rate", "Cache hit rate (0-1)");
        MissRate = Prometheus.Metrics.CreateGauge($"{prefix}cache_miss_rate", "Cache miss rate (0-1)");
        KeysTotal = Prometheus.Metrics.CreateGauge($"{prefix}cache_keys_total", "Total number of keys in cache");
        MemoryUsage = Prometheus.Metrics.CreateGauge($"{prefix}cache_memory_bytes", "Cache memory usage in bytes");
    }

    public void RecordHit(string operation, double durationSeconds)
    {
        Interlocked.Increment(ref _hits);
        OperationDuration.WithLabels(operation).Observe(durationSeconds);
        OperationsTotal.WithLabels(operation, "hit").Inc();
        UpdateRates();
    }

    public void RecordMiss(string operation, double durationSeconds)
    {
        Interlocked.Increment(ref _misses);
        OperationDuration.WithLabels(operation).Observe(durationSeconds);
        OperationsTotal.WithLabels(operation, "miss").Inc();
        UpdateRates();
    }

    public void RecordOperation(string operation, double durationSeconds, bool success)
    {
        OperationDuration.WithLabels(operation).Observe(durationSeconds);
        OperationsTotal.WithLabels(operation, success ? "success" : "error").Inc();
    }

    private void UpdateRates()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var total = hits + misses;
        if (total > 0)
        {
            HitRate.Set((double)hits / total);
            MissRate.Set((double)misses / total);
        }
    }
}
