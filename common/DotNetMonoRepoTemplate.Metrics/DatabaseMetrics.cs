using Prometheus;

namespace DotNetMonoRepoTemplate.Metrics;

public sealed class DatabaseMetrics
{
    private static readonly string[] OperationTableLabels = { "operation", "table" };
    private static readonly string[] OperationTableStatusLabels = { "operation", "table", "status" };
    private static readonly double[] DurationBuckets = { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5 };

    public Histogram QueryDuration { get; }
    public Counter QueryTotal { get; }
    public Gauge ConnectionPoolSize { get; }
    public Gauge ConnectionPoolActive { get; }
    public Gauge ConnectionPoolIdle { get; }

    public DatabaseMetrics(string prefix = "")
    {
        QueryDuration = Prometheus.Metrics.CreateHistogram(
            $"{prefix}database_query_duration_seconds",
            "Database query duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = OperationTableLabels,
                Buckets = DurationBuckets,
            });

        QueryTotal = Prometheus.Metrics.CreateCounter(
            $"{prefix}database_queries_total",
            "Total number of database queries",
            new CounterConfiguration { LabelNames = OperationTableStatusLabels });

        ConnectionPoolSize = Prometheus.Metrics.CreateGauge(
            $"{prefix}database_connection_pool_size",
            "Total size of database connection pool");
        ConnectionPoolActive = Prometheus.Metrics.CreateGauge(
            $"{prefix}database_connection_pool_active",
            "Number of active database connections");
        ConnectionPoolIdle = Prometheus.Metrics.CreateGauge(
            $"{prefix}database_connection_pool_idle",
            "Number of idle database connections");
    }

    public void RecordQuery(string operation, string table, double durationSeconds, bool success)
    {
        QueryDuration.WithLabels(operation, table).Observe(durationSeconds);
        QueryTotal.WithLabels(operation, table, success ? "success" : "error").Inc();
    }
}
