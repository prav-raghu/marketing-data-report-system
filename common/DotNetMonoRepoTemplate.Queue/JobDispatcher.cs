using System.Collections.Concurrent;
using DotNetMonoRepoTemplate.Logging;

namespace DotNetMonoRepoTemplate.Queue;

public delegate Task<JobResult<object?>> JobProcessor<in T>(T payload, JobMetadata? metadata, CancellationToken cancellationToken);

public static class JobHandlerRegistry
{
    private static readonly ConcurrentDictionary<string, Delegate> Handlers = new();

    public static void Register<T>(string queueName, string jobName, JobProcessor<T> processor) =>
        Handlers[Key(queueName, jobName)] = processor;

    internal static JobProcessor<T>? Resolve<T>(string queueName, string jobName) =>
        Handlers.TryGetValue(Key(queueName, jobName), out var handler) ? handler as JobProcessor<T> : null;

    private static string Key(string queueName, string jobName) => $"{queueName}:{jobName}";
}

public sealed class JobDispatcher
{
    private readonly Logger _logger = new(nameof(JobDispatcher));

    public async Task<JobResult<object?>> DispatchAsync<T>(
        string queueName,
        string jobName,
        JobData<T> jobData,
        CancellationToken cancellationToken)
    {
        var handler = JobHandlerRegistry.Resolve<T>(queueName, jobName);
        if (handler is null)
        {
            _logger.Error(
                "No handler registered for job",
                new Dictionary<string, object?> { ["jobName"] = jobName, ["queue"] = queueName });
            return new JobResult<object?> { Success = false, Error = $"No handler registered for job: {jobName}" };
        }

        try
        {
            var result = await handler(jobData.Payload, jobData.Metadata, cancellationToken);
            _logger.Debug(
                "Job processed",
                new Dictionary<string, object?> { ["jobName"] = jobName, ["queue"] = queueName, ["success"] = result.Success });
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(
                "Job processing error",
                new Dictionary<string, object?> { ["jobName"] = jobName, ["queue"] = queueName, ["error"] = ex.Message });
            throw;
        }
    }
}
