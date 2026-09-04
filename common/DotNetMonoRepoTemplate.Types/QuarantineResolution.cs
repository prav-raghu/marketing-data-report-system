using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<QuarantineResolution>))]
public enum QuarantineResolution
{
    Unresolved,
    Reprocessed,
    Discarded,
}
