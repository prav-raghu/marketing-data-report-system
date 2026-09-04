using DotNetMonoRepoTemplate.Ingestion.Connectors;

namespace IngestionApi.Services;

public interface IConnectorRegistry
{
    IReadOnlyCollection<string> RegisteredKeys { get; }

    ISourceConnector Resolve(string sourceKey);

    bool TryResolve(string sourceKey, out ISourceConnector? connector);
}
