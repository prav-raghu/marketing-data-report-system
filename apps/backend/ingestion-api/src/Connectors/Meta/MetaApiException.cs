namespace IngestionApi.Connectors.Meta;

public sealed class MetaApiException : Exception
{
    public MetaApiException(string message, string? reportRunId = null)
        : base(message)
    {
        ReportRunId = reportRunId;
    }

    public string? ReportRunId { get; }
}
