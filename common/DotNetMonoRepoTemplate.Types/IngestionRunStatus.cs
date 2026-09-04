using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<IngestionRunStatus>))]
public enum IngestionRunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}
