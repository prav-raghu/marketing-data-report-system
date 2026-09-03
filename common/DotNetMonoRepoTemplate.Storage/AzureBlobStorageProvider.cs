using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private const string DefaultMimeType = "application/octet-stream";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _defaultContainer;

    public AzureBlobStorageProvider(AzureBlobOptions options)
    {
        _blobServiceClient = new BlobServiceClient(options.ConnectionString);
        _defaultContainer = options.DefaultContainer ?? string.Empty;
    }

    public async Task<UploadResult> UploadFileAsync(
        Stream file,
        UploadOptions options,
        CancellationToken cancellationToken = default)
    {
        var containerName = options.Bucket ?? _defaultContainer;
        var blobName = S3StorageProvider.GenerateKey(options.Folder, options.FileName);

        S3StorageProvider.ValidateFileConstraints(file, options);

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = options.ContentType is not null ? new BlobHttpHeaders { ContentType = options.ContentType } : null,
            Metadata = ToMutableDictionary(options.Metadata),
        };

        await blobClient.UploadAsync(file, uploadOptions, cancellationToken);

        return new UploadResult
        {
            Id = blobName,
            FileName = options.FileName ?? blobName,
            OriginalName = options.FileName ?? blobName,
            MimeType = options.ContentType ?? DefaultMimeType,
            SizeBytes = file.CanSeek ? file.Length : 0,
            Url = blobClient.Uri.ToString(),
            Provider = StorageProvider.AzureBlob,
            Bucket = containerName,
            Key = blobName,
            UploadedAt = DateTime.UtcNow,
            Metadata = options.Metadata,
        };
    }

    public Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var containerName = bucket ?? _defaultContainer;
        var expiresIn = options?.ExpiresIn ?? 3600;
        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(key);
        var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Azure Blob client cannot generate a SAS URI - the connection string must carry account key credentials");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = key,
            Resource = "b",
            ExpiresOn = expiresOn,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(new SignedUrlResult(sasUri.ToString(), expiresOn.UtcDateTime));
    }

    public Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default) =>
        _blobServiceClient
            .GetBlobContainerClient(options.Bucket ?? _defaultContainer)
            .GetBlobClient(options.Key)
            .DeleteAsync(cancellationToken: cancellationToken);

    public async Task<ListFilesResult> ListFilesAsync(
        ListFilesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(options?.Bucket ?? _defaultContainer);

        var files = new List<FileMetadata>();
        string? continuationToken = null;

        await foreach (var page in containerClient
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, options?.Prefix, cancellationToken)
            .AsPages(options?.ContinuationToken, options?.MaxKeys ?? 1000))
        {
            foreach (var blob in page.Values)
            {
                files.Add(new FileMetadata
                {
                    FileName = blob.Name,
                    MimeType = blob.Properties.ContentType ?? DefaultMimeType,
                    SizeBytes = blob.Properties.ContentLength ?? 0,
                    CustomMetadata = ToReadOnlyDictionary(blob.Metadata),
                });
            }
            continuationToken = page.ContinuationToken;
            break;
        }

        return new ListFilesResult
        {
            Files = files,
            IsTruncated = !string.IsNullOrEmpty(continuationToken),
            ContinuationToken = string.IsNullOrEmpty(continuationToken) ? null : continuationToken,
        };
    }

    public async Task<FileMetadata> GetFileMetadataAsync(
        string key,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(bucket ?? _defaultContainer).GetBlobClient(key);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        return new FileMetadata
        {
            FileName = key,
            MimeType = properties.Value.ContentType ?? DefaultMimeType,
            SizeBytes = properties.Value.ContentLength,
            CustomMetadata = ToReadOnlyDictionary(properties.Value.Metadata),
        };
    }

    private static Dictionary<string, string>? ToMutableDictionary(IReadOnlyDictionary<string, string>? source) =>
        source is { Count: > 0 } ? new Dictionary<string, string>(source) : null;

    private static Dictionary<string, string>? ToReadOnlyDictionary(IDictionary<string, string>? source) =>
        source is { Count: > 0 } ? new Dictionary<string, string>(source) : null;
}
