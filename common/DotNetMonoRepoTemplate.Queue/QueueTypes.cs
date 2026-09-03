namespace DotNetMonoRepoTemplate.Queue;

public sealed record JobMetadata
{
    public string? CorrelationId { get; init; }
    public string? UserId { get; init; }
    public string? Source { get; init; }
    public long? Timestamp { get; init; }
}

public sealed record JobData<T>
{
    public required T Payload { get; init; }
    public JobMetadata? Metadata { get; init; }
}

public sealed record JobResult<T>
{
    public required bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
}

public static class JobPriority
{
    public const string Critical = "critical";
    public const string High = "high";
    public const string Normal = "normal";
    public const string Low = "low";

    public static readonly IReadOnlyDictionary<string, int> Values = new Dictionary<string, int>
    {
        [Critical] = 1,
        [High] = 2,
        [Normal] = 3,
        [Low] = 4,
    };
}

public sealed record QueueJobOptions
{
    public string? Priority { get; init; }
    public TimeSpan? Delay { get; init; }
    public int Attempts { get; init; } = 3;
    public string? JobId { get; init; }
}

public sealed record WorkerOptions
{
    public int Concurrency { get; init; } = 5;
}
