namespace DotNetMonoRepoTemplate.Export;

public enum ExportFormat
{
    Csv,
    Excel,
}

public sealed record ExportServiceOptions
{
    public required ExportFormat Format { get; init; }
    public CsvExportOptions? CsvOptions { get; init; }
    public ExcelExportOptions? ExcelOptions { get; init; }
}

public sealed class ExportService
{
    public static Task<byte[]> ExportToBufferAsync<T>(
        IReadOnlyList<T> data,
        ExportServiceOptions options,
        CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?> =>
        options.Format == ExportFormat.Csv
            ? new CsvExporter(options.CsvOptions).ExportToBufferAsync(data, cancellationToken)
            : new ExcelExporter(options.ExcelOptions).ExportToBufferAsync(data, cancellationToken);

    public static async Task<byte[]> StreamExportAsync<T>(
        IAsyncEnumerable<T> dataSource,
        ExportServiceOptions options,
        CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        if (options.Format == ExportFormat.Csv)
        {
            using var memoryStream = new MemoryStream();
            await new CsvExporter(options.CsvOptions).StreamAsync(memoryStream, dataSource, cancellationToken);
            return memoryStream.ToArray();
        }
        return await new ExcelExporter(options.ExcelOptions).StreamToBufferAsync(dataSource, cancellationToken);
    }

    public static string GetContentType(ExportFormat format) =>
        format == ExportFormat.Csv ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string GetFileExtension(ExportFormat format) => format == ExportFormat.Csv ? ".csv" : ".xlsx";
}
