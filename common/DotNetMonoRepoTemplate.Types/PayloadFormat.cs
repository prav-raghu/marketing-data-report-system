using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<PayloadFormat>))]
public enum PayloadFormat
{
    Json,
    Xml,
    Csv,
}
