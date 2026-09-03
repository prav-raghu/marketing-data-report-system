namespace AdminApi.Dtos;

public sealed record ReportFiltersDto
{
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public string? UserId { get; init; }
    public string? Status { get; init; }
}

public sealed record GenerateReportDto
{
    public required string Type { get; init; }
    public required string Format { get; init; }
    public ReportFiltersDto? Filters { get; init; }
    public bool? IncludeHeaders { get; init; }
}

public sealed record StreamReportQueryDto
{
    public required string Type { get; init; }
    public string Format { get; init; } = "csv";
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
}
