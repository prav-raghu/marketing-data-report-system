namespace IngestionApi.Connectors;

public sealed class ConfigurationSecretResolver : IConnectorSecretResolver
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> ResolveAsync(string secretName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var value = _configuration[secretName];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConnectorSecretNotFoundException(secretName);
        }

        return Task.FromResult(value);
    }
}
