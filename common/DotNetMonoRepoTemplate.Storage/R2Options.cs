namespace DotNetMonoRepoTemplate.Storage;

public sealed record R2BucketOptions
{
    public required string Bucket { get; init; }
    public string? PublicBaseUrl { get; init; }
}

public sealed record R2Options
{
    public required string AccountId { get; init; }
    public required string AccessKeyId { get; init; }
    public required string SecretAccessKey { get; init; }
    public required R2BucketOptions PublicBucket { get; init; }
    public required R2BucketOptions PrivateBucket { get; init; }
    public string? Endpoint { get; init; }
    public string? Region { get; init; }
}
