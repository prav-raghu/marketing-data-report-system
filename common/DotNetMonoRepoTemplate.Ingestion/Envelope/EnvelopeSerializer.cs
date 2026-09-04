using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Ingestion.Envelope;

public static class EnvelopeSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(IngestionEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, Options);

    public static IngestionEnvelope? Deserialize(string json) =>
        JsonSerializer.Deserialize<IngestionEnvelope>(json, Options);
}
