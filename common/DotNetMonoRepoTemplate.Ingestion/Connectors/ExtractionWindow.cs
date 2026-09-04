namespace DotNetMonoRepoTemplate.Ingestion.Connectors;

public sealed record ExtractionWindow
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }

    public int DayCount => EndDate.DayNumber - StartDate.DayNumber + 1;

    public static ExtractionWindow Restatement(DateOnly asOfDate, int restatementDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(restatementDays, 1);

        return new ExtractionWindow
        {
            StartDate = asOfDate.AddDays(-(restatementDays - 1)),
            EndDate = asOfDate,
        };
    }

    public static ExtractionWindow Single(DateOnly date) => new() { StartDate = date, EndDate = date };
}
