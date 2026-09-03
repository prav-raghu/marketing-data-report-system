namespace DotNetMonoRepoTemplate.Types;

public sealed record BatchOperationItem<T>(string Id, T Data);

public sealed record BatchOperationResult<T>
{
    public required string Id { get; init; }
    public required bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
}

public sealed record BatchOperationSummary
{
    public required int Total { get; init; }
    public required int Successful { get; init; }
    public required int Failed { get; init; }
    public required IReadOnlyList<BatchOperationResult<object?>> Results { get; init; }
}

public sealed record BatchOperationOptions
{
    public bool? ContinueOnError { get; init; }
    public bool? ValidateBeforeExecute { get; init; }
    public int? MaxBatchSize { get; init; }
}

public static class BatchOperationType
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Custom = "CUSTOM";
}

public sealed record BatchOperation<T>
{
    public required string Type { get; init; }
    public required IReadOnlyList<BatchOperationItem<T>> Items { get; init; }
    public BatchOperationOptions? Options { get; init; }
}
