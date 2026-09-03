using Microsoft.AspNetCore.StaticFiles;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public sealed record StorageServiceOptions
{
    public required string Provider { get; init; }
    public S3Options? S3 { get; init; }
    public AzureBlobOptions? AzureBlob { get; init; }
    public R2Options? R2 { get; init; }
}

public sealed class StorageService
{
    private static readonly FileExtensionContentTypeProvider MimeTypeProvider = new();

    private readonly string _provider;
    private readonly IStorageProvider _activeProvider;
    private readonly R2StorageProvider? _r2Provider;

    public StorageService(StorageServiceOptions options)
    {
        _provider = options.Provider;

        if (options.Provider == StorageProvider.S3 && options.S3 is not null)
        {
            _activeProvider = new S3StorageProvider(options.S3);
        }
        else if (options.Provider == StorageProvider.AzureBlob && options.AzureBlob is not null)
        {
            _activeProvider = new AzureBlobStorageProvider(options.AzureBlob);
        }
        else if (options.Provider == StorageProvider.R2 && options.R2 is not null)
        {
            _r2Provider = new R2StorageProvider(options.R2);
            _activeProvider = _r2Provider;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported storage provider: {options.Provider}");
        }
    }

    public Task<UploadResult> UploadFileAsync(
        Stream file,
        UploadOptions options,
        CancellationToken cancellationToken = default) =>
        _activeProvider.UploadFileAsync(file, options, cancellationToken);

    public Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _activeProvider.GetSignedUrlAsync(key, bucket, options, cancellationToken);

    public string GetPublicUrl(string key) =>
        _r2Provider?.GetPublicUrl(key)
            ?? throw new InvalidOperationException($"GetPublicUrl is not supported for provider: {_provider}");

    public Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default) =>
        _activeProvider.DeleteFileAsync(options, cancellationToken);

    public Task<ListFilesResult> ListFilesAsync(
        ListFilesOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _activeProvider.ListFilesAsync(options, cancellationToken);

    public Task<FileMetadata> GetFileMetadataAsync(
        string key,
        string? bucket = null,
        CancellationToken cancellationToken = default) =>
        _activeProvider.GetFileMetadataAsync(key, bucket, cancellationToken);

    public static bool ValidateFileType(string fileName, IReadOnlyList<string> allowedTypes) =>
        MimeTypeProvider.TryGetContentType(fileName, out var mimeType) && allowedTypes.Contains(mimeType);

    public static bool ValidateFileSize(long sizeBytes, long maxSizeBytes) => sizeBytes <= maxSizeBytes;
}
