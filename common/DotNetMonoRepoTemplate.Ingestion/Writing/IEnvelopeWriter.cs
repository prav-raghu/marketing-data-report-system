using DotNetMonoRepoTemplate.Ingestion.Connectors;

namespace DotNetMonoRepoTemplate.Ingestion.Writing;

public interface IEnvelopeWriter
{
    Task<EnvelopeWriteResult> WriteAsync(
        ExtractionRequest request,
        IAsyncEnumerable<SourceRecord> records,
        CancellationToken cancellationToken);
}
