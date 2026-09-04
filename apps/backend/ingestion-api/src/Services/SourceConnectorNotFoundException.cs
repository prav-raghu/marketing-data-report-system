namespace IngestionApi.Services;

public sealed class SourceConnectorNotFoundException : Exception
{
    public SourceConnectorNotFoundException(string sourceConnectorId)
        : base($"Source connector '{sourceConnectorId}' was not found or is not active.")
    {
        SourceConnectorId = sourceConnectorId;
    }

    public string SourceConnectorId { get; }
}
