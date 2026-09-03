namespace DotNetMonoRepoTemplate.Storage;

public sealed record S3Options
{
    public required string Region { get; init; }
    public required string AccessKeyId { get; init; }
    public required string SecretAccessKey { get; init; }
    public string? DefaultBucket { get; init; }
    public string? Endpoint { get; init; }
}
