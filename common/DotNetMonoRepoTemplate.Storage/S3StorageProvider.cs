using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public sealed class S3StorageProvider : IStorageProvider, IDisposable
{
    private const string DefaultMimeType = "application/octet-stream";

    private readonly IAmazonS3 _client;
    private readonly string _defaultBucket;

    public S3StorageProvider(S3Options options)
    {
        var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region) };
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            config.ServiceURL = options.Endpoint;
        }
        _client = new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
        _defaultBucket = options.DefaultBucket ?? string.Empty;
    }

    public async Task<UploadResult> UploadFileAsync(
        Stream file,
        UploadOptions options,
        CancellationToken cancellationToken = default)
    {
        var bucket = options.Bucket ?? _defaultBucket;
        var key = GenerateKey(options.Folder, options.FileName);

        ValidateFileConstraints(file, options);

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = file,
            ContentType = options.ContentType,
            CannedACL = MapAccessLevel(options.AccessLevel),
        };
        AddMetadata(request.Metadata, options.Metadata);

        await _client.PutObjectAsync(request, cancellationToken);

        return new UploadResult
        {
            Id = key,
            FileName = options.FileName ?? key,
            OriginalName = options.FileName ?? key,
            MimeType = options.ContentType ?? DefaultMimeType,
            SizeBytes = file.CanSeek ? file.Length : 0,
            Url = $"https://{bucket}.s3.amazonaws.com/{key}",
            Provider = StorageProvider.S3,
            Bucket = bucket,
            Key = key,
            UploadedAt = DateTime.UtcNow,
            Metadata = options.Metadata,
        };
    }

    public async Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var targetBucket = bucket ?? _defaultBucket;
        var expiresIn = options?.ExpiresIn ?? 3600;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = targetBucket,
            Key = key,
            Expires = DateTime.UtcNow.AddSeconds(expiresIn),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = options?.ResponseContentType,
                ContentDisposition = options?.ResponseContentDisposition,
            },
        };

        var url = await _client.GetPreSignedURLAsync(request);
        return new SignedUrlResult(url, DateTime.UtcNow.AddSeconds(expiresIn));
    }

    public Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(options.Bucket ?? _defaultBucket, options.Key, cancellationToken);

    public async Task<ListFilesResult> ListFilesAsync(
        ListFilesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = options?.Bucket ?? _defaultBucket,
            Prefix = options?.Prefix,
            MaxKeys = options?.MaxKeys ?? 1000,
            ContinuationToken = options?.ContinuationToken,
        };

        var response = await _client.ListObjectsV2Async(request, cancellationToken);

        var files = response.S3Objects
            .Select(item => new FileMetadata
            {
                FileName = item.Key,
                MimeType = DefaultMimeType,
                SizeBytes = item.Size ?? 0,
                CustomMetadata = new Dictionary<string, string>
                {
                    ["lastModified"] = item.LastModified?.ToString("O") ?? string.Empty,
                    ["etag"] = item.ETag ?? string.Empty,
                },
            })
            .ToList();

        return new ListFilesResult
        {
            Files = files,
            IsTruncated = response.IsTruncated ?? false,
            ContinuationToken = response.NextContinuationToken,
        };
    }

    public async Task<FileMetadata> GetFileMetadataAsync(
        string key,
        string? bucket = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectMetadataAsync(bucket ?? _defaultBucket, key, cancellationToken);

        return new FileMetadata
        {
            FileName = key,
            MimeType = response.Headers.ContentType ?? DefaultMimeType,
            SizeBytes = response.Headers.ContentLength,
            CustomMetadata = response.Metadata.Keys.Count > 0
                ? response.Metadata.Keys.ToDictionary(metaKey => metaKey, metaKey => response.Metadata[metaKey])
                : null,
        };
    }

    internal static string GenerateKey(string? folder, string? fileName)
    {
        var key = fileName
            ?? $"file_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(9)).ToLowerInvariant()}";
        return string.IsNullOrWhiteSpace(folder) ? key : $"{folder}/{key}";
    }

    internal static void ValidateFileConstraints(Stream file, UploadOptions options)
    {
        if (options.MaxSizeBytes.HasValue && file.CanSeek && file.Length > options.MaxSizeBytes.Value)
        {
            throw new InvalidOperationException($"File size exceeds maximum allowed size of {options.MaxSizeBytes} bytes");
        }
        if (options.AllowedMimeTypes is not null
            && options.ContentType is not null
            && !options.AllowedMimeTypes.Contains(options.ContentType))
        {
            throw new InvalidOperationException($"File type {options.ContentType} is not allowed");
        }
    }

    internal static void AddMetadata(MetadataCollection collection, IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return;
        }
        foreach (var (metaKey, metaValue) in metadata)
        {
            collection.Add(metaKey, metaValue);
        }
    }

    private static S3CannedACL MapAccessLevel(string? accessLevel)
    {
        if (accessLevel == FileAccessLevel.Public)
        {
            return S3CannedACL.PublicRead;
        }
        if (accessLevel == FileAccessLevel.Authenticated)
        {
            return S3CannedACL.AuthenticatedRead;
        }
        return S3CannedACL.Private;
    }

    public void Dispose() => _client.Dispose();
}
