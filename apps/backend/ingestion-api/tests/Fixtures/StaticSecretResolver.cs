using IngestionApi.Connectors;

namespace IngestionApi.Tests.Fixtures;

public sealed class StaticSecretResolver : IConnectorSecretResolver
{
    private readonly string _value;

    public StaticSecretResolver(string value)
    {
        _value = value;
    }

    public List<string> RequestedSecretNames { get; } = [];

    public Task<string> ResolveAsync(string secretName, CancellationToken cancellationToken)
    {
        RequestedSecretNames.Add(secretName);
        return Task.FromResult(_value);
    }
}
