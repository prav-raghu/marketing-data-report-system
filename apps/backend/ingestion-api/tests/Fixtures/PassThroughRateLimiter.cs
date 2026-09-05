using IngestionApi.RateLimiting;

namespace IngestionApi.Tests.Fixtures;

public sealed class PassThroughRateLimiter : IRateLimiter
{
    public List<string> AcquiredPartitions { get; } = [];

    public Task AcquireAsync(string partitionKey, CancellationToken cancellationToken)
    {
        AcquiredPartitions.Add(partitionKey);
        return Task.CompletedTask;
    }
}
