namespace IngestionApi.Connectors;

public interface IConnectorSecretResolver
{
    public Task<string> ResolveAsync(string secretName, CancellationToken cancellationToken);
}
