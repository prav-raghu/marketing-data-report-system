using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<IngestionRunTrigger>))]
public enum IngestionRunTrigger
{
    Scheduled,
    Manual,
    Backfill,
}
