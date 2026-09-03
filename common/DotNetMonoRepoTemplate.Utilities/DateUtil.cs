using System.Globalization;

namespace DotNetMonoRepoTemplate.Utilities;

public static class DateUtil
{
    public static string ToIsoString(DateTime date) =>
        date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    public static DateTime? ParseIsoDate(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    public static bool IsValidIsoDate(string value) => ParseIsoDate(value) is not null;

    public static bool IsFuture(DateTime date) => date.ToUniversalTime() > DateTime.UtcNow;

    public static bool IsPast(DateTime date) => date.ToUniversalTime() < DateTime.UtcNow;

    public static DateTime AddDays(DateTime date, int days) => date.AddDays(days);

    public static DateTime StartOfUtcDay(DateTime date)
    {
        var utc = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime EndOfUtcDay(DateTime date)
    {
        var utc = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return new DateTime(utc.Year, utc.Month, utc.Day, 23, 59, 59, 999, DateTimeKind.Utc);
    }
}
