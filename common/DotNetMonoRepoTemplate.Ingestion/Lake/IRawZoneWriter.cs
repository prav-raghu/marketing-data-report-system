namespace DotNetMonoRepoTemplate.Ingestion.Lake;

public interface IRawZoneWriter
{
    public Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
