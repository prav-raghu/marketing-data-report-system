namespace IngestionApi.RateLimiting;

public interface IRateLimiter
{
    public Task AcquireAsync(string partitionKey, CancellationToken cancellationToken);
}
