using Azure;
using Azure.Storage.Blobs;
using DotNetMonoRepoTemplate.Logging;

namespace DotNetMonoRepoTemplate.Ingestion.Lake;

public sealed class BlobRawZoneWriter : IRawZoneWriter
{
    private readonly BlobContainerClient _container;
    private readonly Logger _logger = new("BlobRawZoneWriter");

    public BlobRawZoneWriter(RawZoneOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
    }

    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var blob = _container.GetBlobClient(path);

        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            await blob.UploadAsync(stream, overwrite: false, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            _logger.Warn(
                "Raw zone part already exists and was not overwritten",
                new Dictionary<string, object?> { ["path"] = path });
        }
    }
}
