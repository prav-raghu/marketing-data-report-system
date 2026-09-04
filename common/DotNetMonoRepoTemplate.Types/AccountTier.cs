using System.Text.Json.Serialization;

namespace DotNetMonoRepoTemplate.Types;

[JsonConverter(typeof(JsonStringEnumConverter<AccountTier>))]
public enum AccountTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
}
