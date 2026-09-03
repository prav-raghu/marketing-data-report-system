namespace DotNetMonoRepoTemplate.Types;

public static class StorageProvider
{
    public const string S3 = "S3";
    public const string AzureBlob = "AZURE_BLOB";
    public const string R2 = "R2";
    public const string Local = "LOCAL";
}

public static class FileAccessLevel
{
    public const string Public = "PUBLIC";
    public const string Private = "PRIVATE";
    public const string Authenticated = "AUTHENTICATED";
}

public sealed record UploadOptions
{
    public string? Bucket { get; init; }
    public string? Folder { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public long? MaxSizeBytes { get; init; }
    public IReadOnlyList<string>? AllowedMimeTypes { get; init; }
    public string? AccessLevel { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record UploadResult
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string OriginalName { get; init; }
    public required string MimeType { get; init; }
    public required long SizeBytes { get; init; }
    public required string Url { get; init; }
    public required string Provider { get; init; }
    public string? Bucket { get; init; }
    public string? Key { get; init; }
    public required DateTime UploadedAt { get; init; }
    public bool? IsPublic { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record SignedUrlOptions
{
    public int? ExpiresIn { get; init; }
    public string? ResponseContentType { get; init; }
    public string? ResponseContentDisposition { get; init; }
}

public sealed record SignedUrlResult(string Url, DateTime ExpiresAt);

public sealed record FileMetadata
{
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
    public required long SizeBytes { get; init; }
    public string? UploadedBy { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyDictionary<string, string>? CustomMetadata { get; init; }
}

public sealed record DeleteFileOptions
{
    public string? Bucket { get; init; }
    public required string Key { get; init; }
}

public sealed record ListFilesOptions
{
    public string? Bucket { get; init; }
    public string? Prefix { get; init; }
    public int? MaxKeys { get; init; }
    public string? ContinuationToken { get; init; }
}

public sealed record ListFilesResult
{
    public required IReadOnlyList<FileMetadata> Files { get; init; }
    public string? ContinuationToken { get; init; }
    public required bool IsTruncated { get; init; }
}
