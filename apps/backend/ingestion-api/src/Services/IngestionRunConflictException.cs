namespace IngestionApi.Services;

public sealed class IngestionRunConflictException : Exception
{
    public IngestionRunConflictException(string sourceConnectorId)
        : base($"Source connector '{sourceConnectorId}' already has a run in flight.")
    {
        SourceConnectorId = sourceConnectorId;
    }

    public string SourceConnectorId { get; }
}
