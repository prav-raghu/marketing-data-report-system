namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public interface ISourceConnector
{
    string SourceKey { get; }

    SourceCapabilities Capabilities { get; }

    IAsyncEnumerable<SourceRecord> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken);
}
