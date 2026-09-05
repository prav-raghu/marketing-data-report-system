namespace IngestionApi.RateLimiting;

public sealed class RateLimitTimeoutException : Exception
{
    public RateLimitTimeoutException(string partitionKey, TimeSpan timeout)
        : base($"Timed out after {timeout} waiting for a rate limit permit on partition '{partitionKey}'.")
    {
        PartitionKey = partitionKey;
    }

    public string PartitionKey { get; }
}
