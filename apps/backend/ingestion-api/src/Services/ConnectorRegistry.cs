using DotNetMonoRepoTemplate.Ingestion.Connectors;

namespace IngestionApi.Services;

public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<string, ISourceConnector> _connectors;

    public ConnectorRegistry(IEnumerable<ISourceConnector> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);

        _connectors = new Dictionary<string, ISourceConnector>(StringComparer.OrdinalIgnoreCase);

        foreach (var connector in connectors)
        {
            if (!_connectors.TryAdd(connector.SourceKey, connector))
            {
                throw new InvalidOperationException(
                    $"Duplicate source connector key '{connector.SourceKey}'. Every connector must register a unique key.");
            }
        }
    }

    public IReadOnlyCollection<string> RegisteredKeys => _connectors.Keys;

    public ISourceConnector Resolve(string sourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        if (!_connectors.TryGetValue(sourceKey, out var connector))
        {
            throw new InvalidOperationException(
                $"No connector is registered for source key '{sourceKey}'. Registered keys: {string.Join(", ", _connectors.Keys)}");
        }

        return connector;
    }

    public bool TryResolve(string sourceKey, out ISourceConnector? connector)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            connector = null;
            return false;
        }

        return _connectors.TryGetValue(sourceKey, out connector);
    }
}
