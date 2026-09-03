# Export Functionality Documentation

## Overview

The export system provides CSV and Excel export capabilities across all backend services. Streaming here means "build the buffer from an `IAsyncEnumerable` data source" — not true chunked HTTP streaming; see "Known limitation" below before assuming this scales the way the Node original's `reply.raw.write(chunk)` loop did.

## Library: `DotNetMonoRepoTemplate.Export`

Located in `common/DotNetMonoRepoTemplate.Export`, this library provides:
- **`CsvExporter`**: CSV generation via CsvHelper
- **`ExcelExporter`**: XLSX format with styling and formatting via ClosedXML
- **`ExportService`**: Unified interface for both formats
- **`StreamExportAsync`**: Builds from an `IAsyncEnumerable<T>` data source — see the limitation below

## Known limitation — not true streaming, unlike the Node original

`ExportService.StreamExportAsync` buffers the **whole export server-side** before returning a `byte[]`, rather than writing chunks progressively to the HTTP response the way the Node original's `reply.raw.write(chunk)` loop did with ExcelJS/csv-stringify. It produces the same file — the actual streaming/memory-efficiency benefit for very large exports isn't there yet. This is a known, carried-over scope-narrowing from the migration, not an oversight in how it was wired into endpoints. Also: **ClosedXML (the Excel library) has no true streaming API at all** — `ExcelExporter` always builds the full workbook in memory regardless of how it's called, so `StreamExportAsync` with `Format = ExportFormat.Excel` gains nothing over `ExportToBufferAsync` today.

If a task genuinely needs memory-efficient large-CSV export, `CsvExporter.StreamAsync` (the lower-level method, not `ExportService.StreamExportAsync`) does write directly to a provided `Stream` as it iterates the `IAsyncEnumerable` — that's the one real streaming path, and it's CSV-only.

## Types

`T` in every export method must implement `IReadOnlyDictionary<string, object?>` — typically `Dictionary<string, object?>` built via a `.Select()` projection from an EF Core query. This is why report/export records are dictionaries rather than typed DTOs in `ReportingService`/`ExportEndpoints` — the export library is generic over row shape, not over a specific entity type.

## Usage Examples

### Basic Buffer Export (Small Datasets)

```csharp
var exportService = new ExportService();

var buffer = await exportService.ExportToBufferAsync(data, new ExportServiceOptions
{
    Format = ExportFormat.Excel,
    ExcelOptions = new ExcelExportOptions { SheetName = "Users", StyleHeader = true, FreezeHeader = true },
});

return Results.File(buffer, exportService.GetContentType(ExportFormat.Excel), $"users{exportService.GetFileExtension(ExportFormat.Excel)}");
```

### "Streaming" Export (builds from an async data source, still buffers server-side — see limitation above)

```csharp
async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> FetchUsersStreamAsync(AppDbContext db, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    const int batchSize = 100;
    var page = 0;
    while (true)
    {
        var batch = await db.Users.Skip(page * batchSize).Take(batchSize)
            .Select(u => new Dictionary<string, object?> { ["id"] = u.Id, ["email"] = u.Email })
            .ToListAsync(cancellationToken);
        if (batch.Count == 0) { yield break; }
        foreach (var row in batch) { yield return row; }
        page++;
    }
}

var buffer = await exportService.StreamExportAsync(FetchUsersStreamAsync(db, cancellationToken), new ExportServiceOptions { Format = ExportFormat.Csv });
await context.Response.Body.WriteAsync(buffer, context.RequestAborted);
```

### Real implementations

See `CustomerApi.Endpoints.ExportEndpoints` (`/api/v1/users/export`, `/api/v1/users/export/stream`) and `AdminApi.Services.ReportingService`/`AdminApi.Endpoints.ReportingEndpoints` (`/api/v1/reports/generate`, `/api/v1/reports/stream`) for complete, currently-running examples — including query-parameter handling, `Content-Type`/`Content-Disposition` headers, and error handling.

## Configuration Options

### CSV Options (`CsvExportOptions`)

```csharp
public sealed record CsvExportOptions
{
    public IReadOnlyList<string>? Headers { get; init; }
    public string Delimiter { get; init; } = ",";
    public bool Bom { get; init; }
}
```

### Excel Options (`ExcelExportOptions`)

```csharp
public sealed record ExcelExportOptions
{
    public string SheetName { get; init; } = "Sheet1";
    public IReadOnlyList<string>? Headers { get; init; }
    public IReadOnlyList<double>? ColumnWidths { get; init; }
    public bool FreezeHeader { get; init; } = true;
    public bool AutoFilter { get; init; } = true;
    public bool StyleHeader { get; init; } = true;
}
```

## Performance Considerations

### When to Use Buffer Export (`ExportToBufferAsync`)

- Small-to-medium datasets — given the streaming limitation above, this and `StreamExportAsync` have near-identical memory characteristics today for anything already materialized as a `List`
- Need to return `Content-Length`

### When `StreamExportAsync`/`CsvExporter.StreamAsync` still help

- The data source is itself paginated/lazily fetched (avoids materializing the *source* query all at once, even though the *output* buffer is still built in memory)
- CSV specifically, via `CsvExporter.StreamAsync` directly — the one path with real streaming semantics

## Integration with Services

### Customer API
Export user data (`ExportEndpoints`)

### Admin API
Export system metrics, webhook delivery reports, user activity reports (`ReportingService`/`ReportingEndpoints`)

### Schedule API
No export integration currently

## Adding Export to a New Endpoint

1. **Add a `<ProjectReference>`** to `DotNetMonoRepoTemplate.Export.csproj` in the service's `.csproj`
2. **Build the data as `IReadOnlyDictionary<string, object?>` rows** via a `.Select()` projection
3. **Call `ExportService.ExportToBufferAsync`/`StreamExportAsync`**, set `Content-Type`/`Content-Disposition`, write the buffer to the response

```csharp
group.MapGet("/export", async (AppDbContext db, string? format) =>
{
    var exportFormat = format == "excel" ? ExportFormat.Excel : ExportFormat.Csv;
    var data = await db.SomeEntities.Select(e => new Dictionary<string, object?> { ["id"] = e.Id }).ToListAsync();
    var exportService = new ExportService();
    var buffer = await exportService.ExportToBufferAsync(data, new ExportServiceOptions { Format = exportFormat });
    return Results.File(buffer, exportService.GetContentType(exportFormat), $"export{exportService.GetFileExtension(exportFormat)}");
});
```

## Best Practices

1. Use `.Select()` projections to build export rows — never materialize full entities just to export a subset of fields
2. Set appropriate headers: `Content-Type`, `Content-Disposition` (with filename)
3. Implement proper error handling (most of this is caught centrally by `AppExceptionHandler` — see `backend-service.md`)
4. Paginate the *data source* even though the export buffer itself isn't truly streamed
5. Test with production-sized datasets — the buffering limitation above means large exports have real memory cost, worth load-testing before shipping a large-dataset export feature
6. Rate-limit export endpoints (`sensitive`/`adminOperations` tier — see `rules/backend.md`)
7. Gate export endpoints with `RequirePermissionsAttribute` (`ReportExport`, or the relevant domain permission)
8. Consider audit-logging export operations (see `audit-log.md`) if the exported data is sensitive

## NuGet Dependencies

- **CsvHelper**: CSV generation
- **ClosedXML**: Excel file generation (no true streaming — see limitation above)

## Testing

```csharp
var exportService = new ExportService();
var testData = new List<Dictionary<string, object?>>
{
    new() { ["id"] = 1, ["name"] = "John" },
    new() { ["id"] = 2, ["name"] = "Jane" },
};

var buffer = await exportService.ExportToBufferAsync(testData, new ExportServiceOptions { Format = ExportFormat.Csv });

Assert.NotEmpty(buffer);
Assert.Contains("John", Encoding.UTF8.GetString(buffer));
```
