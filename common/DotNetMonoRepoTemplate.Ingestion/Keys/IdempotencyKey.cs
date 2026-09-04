using System.Globalization;
using System.Text;

namespace DotNetMonoRepoTemplate.Ingestion.Keys;

public static class IdempotencyKey
{
    private const char ComponentSeparator = '|';
    private const string EscapedSeparator = "%7C";
    private const char BreakdownSeparator = ':';
    private const string EscapedBreakdownSeparator = "%3A";

    public static string Create(
        string sourceSystem,
        string accountId,
        string entityLevel,
        string entityId,
        DateOnly metricDate,
        IReadOnlyDictionary<string, string>? breakdowns = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityLevel);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var builder = new StringBuilder();
        builder.Append(Escape(sourceSystem));
        Append(builder, accountId);
        Append(builder, entityLevel);
        Append(builder, entityId);
        Append(builder, metricDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if (breakdowns is { Count: > 0 })
        {
            foreach (var breakdown in breakdowns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                Append(builder, $"{EscapeBreakdown(breakdown.Key)}{BreakdownSeparator}{EscapeBreakdown(breakdown.Value)}");
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string component)
    {
        builder.Append(ComponentSeparator);
        builder.Append(Escape(component));
    }

    private static string Escape(string value) =>
        value.Replace(ComponentSeparator.ToString(), EscapedSeparator, StringComparison.Ordinal);

    private static string EscapeBreakdown(string value) =>
        Escape(value).Replace(BreakdownSeparator.ToString(), EscapedBreakdownSeparator, StringComparison.Ordinal);
}
