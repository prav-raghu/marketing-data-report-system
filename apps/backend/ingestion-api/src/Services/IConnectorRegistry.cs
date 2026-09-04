using DotNetMonoRepoTemplate.Ingestion.Connectors;

namespace IngestionApi.Services;

public interface IConnectorRegistry
{
    public IReadOnlyCollection<string> RegisteredKeys { get; }

    public ISourceConnector Resolve(string sourceKey);

    public bool TryResolve(string sourceKey, out ISourceConnector? connector);
}
