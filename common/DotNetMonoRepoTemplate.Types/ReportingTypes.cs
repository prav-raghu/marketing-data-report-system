namespace DotNetMonoRepoTemplate.Types;

public static class ReportType
{
    public const string UserActivity = "USER_ACTIVITY";
    public const string SystemMetrics = "SYSTEM_METRICS";
    public const string AuditLog = "AUDIT_LOG";
    public const string WebhookDelivery = "WEBHOOK_DELIVERY";
    public const string Custom = "CUSTOM";
}

public static class ReportFormat
{
    public const string Csv = "CSV";
    public const string Excel = "EXCEL";
    public const string Json = "JSON";
    public const string Pdf = "PDF";
}

public static class ReportStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

public sealed record ReportFilter
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? UserId { get; init; }
    public string? Status { get; init; }
    public IReadOnlyDictionary<string, object?>? AdditionalFilters { get; init; }
}

public sealed record ReportRequest
{
    public required string Type { get; init; }
    public required string Format { get; init; }
    public ReportFilter? Filters { get; init; }
    public bool? IncludeHeaders { get; init; }
    public IReadOnlyList<string>? GroupBy { get; init; }
}

public sealed record ReportResult
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Format { get; init; }
    public required string Status { get; init; }
    public string? Url { get; init; }
    public string? Error { get; init; }
    public int? RecordCount { get; init; }
    public DateTime? GeneratedAt { get; init; }
}

public sealed record ScheduledReport
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Format { get; init; }
    public required string Schedule { get; init; }
    public ReportFilter? Filters { get; init; }
    public required bool Enabled { get; init; }
    public IReadOnlyList<string>? Recipients { get; init; }
}
