namespace DotNetMonoRepoTemplate.Utilities;

public static class DurationParser
{
    public static TimeSpan Parse(string? value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return fallback;
        }

        var unit = value[^1];
        if (!double.TryParse(value[..^1], out var amount) || amount <= 0)
        {
            return fallback;
        }

        return unit switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            _ => fallback,
        };
    }
}
