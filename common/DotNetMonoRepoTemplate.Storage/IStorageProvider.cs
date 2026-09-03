using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public interface IStorageProvider
{
    Task<UploadResult> UploadFileAsync(Stream file, UploadOptions options, CancellationToken cancellationToken = default);

    Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default);

    Task<ListFilesResult> ListFilesAsync(ListFilesOptions? options = null, CancellationToken cancellationToken = default);

    Task<FileMetadata> GetFileMetadataAsync(string key, string? bucket = null, CancellationToken cancellationToken = default);
}
