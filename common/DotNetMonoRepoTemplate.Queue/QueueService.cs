using Hangfire;
using DotNetMonoRepoTemplate.Logging;

namespace DotNetMonoRepoTemplate.Queue;

public sealed class QueueService<T>
{
    private readonly string _queueName;
    private readonly IBackgroundJobClient _client;
    private readonly Logger _logger;

    public QueueService(string queueName, IBackgroundJobClient client)
    {
        _queueName = queueName;
        _client = client;
        _logger = new Logger($"QueueService:{queueName}");
    }

    public string Enqueue(string jobName, T payload, QueueJobOptions? options = null)
    {
        var jobData = new JobData<T>
        {
            Payload = payload,
            Metadata = new JobMetadata { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
        };

        var dispatcher = new JobDispatcher();

        var jobId = options?.Delay is { } delay && delay > TimeSpan.Zero
            ? _client.Schedule<JobDispatcher>(d => d.DispatchAsync(_queueName, jobName, jobData, CancellationToken.None), delay)
            : _client.Enqueue<JobDispatcher>(d => d.DispatchAsync(_queueName, jobName, jobData, CancellationToken.None));

        _ = dispatcher;
        _logger.Debug(
            "Job enqueued",
            new Dictionary<string, object?> { ["jobName"] = jobName, ["queue"] = _queueName, ["jobId"] = jobId });
        return jobId;
    }
}
