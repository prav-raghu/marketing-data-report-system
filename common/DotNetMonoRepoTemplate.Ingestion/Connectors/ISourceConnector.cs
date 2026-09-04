namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public interface ISourceConnector
{
    public string SourceKey { get; }

    public SourceCapabilities Capabilities { get; }

    public IAsyncEnumerable<SourceRecord> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken);
}
