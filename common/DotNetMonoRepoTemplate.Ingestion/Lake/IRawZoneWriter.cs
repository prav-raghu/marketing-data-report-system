namespace DotNetMonoRepoTemplate.Ingestion.Lake;

public interface IRawZoneWriter
{
    Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
