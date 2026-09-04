using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotNetMonoRepoTemplate.Ingestion.Keys;

public static class CanonicalJsonWriter
{
    public static byte[] Write(JsonNode? node)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            WriteNode(writer, node);
        }

        return buffer.ToArray();
    }

    private static void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;

            case JsonObject jsonObject:
                writer.WriteStartObject();
                foreach (var property in jsonObject.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteNode(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonArray jsonArray:
                writer.WriteStartArray();
                foreach (var item in jsonArray)
                {
                    WriteNode(writer, item);
                }
                writer.WriteEndArray();
                break;

            default:
                node.WriteTo(writer);
                break;
        }
    }
}
