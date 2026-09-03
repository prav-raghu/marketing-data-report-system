using DotNetMonoRepoTemplate.Types;

namespace DotNetMonoRepoTemplate.Storage;

public interface IStorageProvider
{
    public Task<UploadResult> UploadFileAsync(Stream file, UploadOptions options, CancellationToken cancellationToken = default);

    public Task<SignedUrlResult> GetSignedUrlAsync(
        string key,
        string? bucket = null,
        SignedUrlOptions? options = null,
        CancellationToken cancellationToken = default);

    public Task DeleteFileAsync(DeleteFileOptions options, CancellationToken cancellationToken = default);

    public Task<ListFilesResult> ListFilesAsync(ListFilesOptions? options = null, CancellationToken cancellationToken = default);

    public Task<FileMetadata> GetFileMetadataAsync(string key, string? bucket = null, CancellationToken cancellationToken = default);
}
