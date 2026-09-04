using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<MetricAdditivity>))]
public enum MetricAdditivity
{
    Additive,
    SemiAdditive,
    NonAdditive,
}
