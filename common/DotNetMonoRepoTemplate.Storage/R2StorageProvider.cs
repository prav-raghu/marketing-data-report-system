using Amazon.S3;
using Amazon.S3.Model;
using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public sealed class R2StorageProvider : IStorageProvider, IDisposable
{
    private const string DefaultMimeType = "application/octet-stream";

    private readonly AmazonS3Client _client;
    private readonly R2BucketOptions _publicBucket;
    private readonly R2BucketOptions _privateBucket;

    public R2StorageProvider(R2Options options)
    {
        var endpoint = options.Endpoint ?? $"https://{options.AccountId}.r2.cloudflarestorage.com";
        var config = new AmazonS3Config
        {
            RegionEndpoint = null,
            ServiceURL = endpoint,
            AuthenticationRegion = options.Region ?? "auto",
        };
        _client = new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
        _publicBucket = options.PublicBucket;
        _privateBucket = options.PrivateBucket;
    }

    public async Task<UploadResult> UploadFileAsync(
        Stream file,
        UploadOptions options,
        CancellationToken cancellationToken = default)
    {
        var isPublic = options.AccessLevel == FileAccessLevel.Public;
        var target = ResolveBucket(options.Bucket, isPublic);
        var key = S3StorageProvider.GenerateKey(options.Folder, options.FileName);

        S3StorageProvider.ValidateFileConstraints(file, options);

        var request = new PutObjectRequest
        {
            BucketName = target.Bucket,
            Key = key,
            InputStream = file,
            ContentType = options.ContentType,
        };
        S3StorageProvider.AddMetadata(request.Metadata, options.Metadata);

        await _client.PutObjectAsync(request, cancellationToken);

        var url = isPublic ? BuildPublicUrl(target, key) : (await GetSignedUrlAsync(key, target.Bucket, cancellationToken: cancellationToken)).Url;

        return new UploadResult
        {
            Id = key,
            FileName = options.FileName ?? key,
            OriginalName = options.FileName ?? key,
            MimeType = options.ContentType ?? DefaultMimeType,
            SizeBytes = file.CanSeek ? file.Length : 0,
            Url = url,
            Provider = StorageProvider.R2,
            Bucket = target.Bucket,
            Key = key,
            UploadedAt = DateTime.UtcNow,
            IsPublic = isPublic,
            Metadata = options.Metadata,
        };
    }

    public async Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var targetBucket = bucket ?? _privateBucket.Bucket;
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

    public string GetPublicUrl(string key) => BuildPublicUrl(_publicBucket, key);

    public Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(options.Bucket ?? _privateBucket.Bucket, options.Key, cancellationToken);

    public async Task<ListFilesResult> ListFilesAsync(
        ListFilesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = options?.Bucket ?? _privateBucket.Bucket,
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
        var response = await _client.GetObjectMetadataAsync(bucket ?? _privateBucket.Bucket, key, cancellationToken);

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

    private R2BucketOptions ResolveBucket(string? bucket, bool isPublic)
    {
        if (string.IsNullOrWhiteSpace(bucket))
        {
            return isPublic ? _publicBucket : _privateBucket;
        }
        var basis = isPublic ? _publicBucket : _privateBucket;
        return new R2BucketOptions { Bucket = bucket, PublicBaseUrl = basis.PublicBaseUrl };
    }

    private static string BuildPublicUrl(R2BucketOptions target, string key)
    {
        if (string.IsNullOrWhiteSpace(target.PublicBaseUrl))
        {
            throw new InvalidOperationException("Public R2 bucket has no PublicBaseUrl configured");
        }
        return $"{target.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public void Dispose() => _client.Dispose();
}
