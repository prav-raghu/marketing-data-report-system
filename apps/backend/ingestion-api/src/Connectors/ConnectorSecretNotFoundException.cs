namespace IngestionApi.Connectors;

public sealed class ConnectorSecretNotFoundException : Exception
{
    public ConnectorSecretNotFoundException(string secretName)
        : base($"Connector secret '{secretName}' could not be resolved.")
    {
        SecretName = secretName;
    }

    public string SecretName { get; }
}
