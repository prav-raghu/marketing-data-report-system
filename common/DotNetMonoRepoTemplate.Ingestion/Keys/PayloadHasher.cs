using System.Security.Cryptography;
using System.Text.Json.Nodes;
using DotNetMonoRepoTemplate.Ingestion.Envelope;

namespace DotNetMonoRepoTemplate.Ingestion.Keys;

public static class PayloadHasher
{
    public static string ComputeHash(JsonNode? payload)
    {
        var canonical = CanonicalJsonWriter.Write(payload);
        var hash = SHA256.HashData(canonical);
        return EnvelopeConstants.HashPrefix + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
