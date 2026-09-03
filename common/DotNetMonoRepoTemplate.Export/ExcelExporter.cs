using ClosedXML.Excel;

namespace DotNetMonoRepoTemplate.Export;

public sealed record ExcelExportOptions
{
    public string SheetName { get; init; } = "Sheet1";
    public IReadOnlyList<string>? Headers { get; init; }
    public IReadOnlyList<double>? ColumnWidths { get; init; }
    public bool FreezeHeader { get; init; } = true;
    public bool AutoFilter { get; init; } = true;
    public bool StyleHeader { get; init; } = true;
}

public sealed class ExcelExporter
{
    private readonly ExcelExportOptions _options;

    public ExcelExporter(ExcelExportOptions? options = null) => _options = options ?? new ExcelExportOptions();

    public Task<byte[]> ExportToBufferAsync<T>(IReadOnlyList<T> data, CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(_options.SheetName);

        if (data.Count > 0)
        {
            var columns = _options.Headers ?? data[0].Keys.ToList();
            WriteHeader(worksheet, columns);
            WriteRows(worksheet, columns, data);
            ApplyFormatting(worksheet, columns.Count);
        }

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return Task.FromResult(memoryStream.ToArray());
    }

    public async Task<byte[]> StreamToBufferAsync<T>(
        IAsyncEnumerable<T> dataSource,
        CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        var buffered = new List<T>();
        await foreach (var record in dataSource.WithCancellation(cancellationToken))
        {
            buffered.Add(record);
        }
        return await ExportToBufferAsync(buffered, cancellationToken);
    }

    private void WriteHeader(IXLWorksheet worksheet, IReadOnlyList<string> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = columns[i];
            worksheet.Column(i + 1).Width = _options.ColumnWidths is { Count: > 0 } widths && i < widths.Count
                ? widths[i]
                : 15;
        }
    }

    private static void WriteRows<T>(IXLWorksheet worksheet, IReadOnlyList<string> columns, IReadOnlyList<T> data)
        where T : IReadOnlyDictionary<string, object?>
    {
        for (var rowIndex = 0; rowIndex < data.Count; rowIndex++)
        {
            var record = data[rowIndex];
            for (var colIndex = 0; colIndex < columns.Count; colIndex++)
            {
                if (record.TryGetValue(columns[colIndex], out var value))
                {
                    SetCellValue(worksheet.Cell(rowIndex + 2, colIndex + 1), value);
                }
            }
        }
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear();
                break;
            case string stringValue:
                cell.Value = stringValue;
                break;
            case bool boolValue:
                cell.Value = boolValue;
                break;
            case DateTime dateValue:
                cell.Value = dateValue;
                break;
            case int or long or double or decimal or float:
                cell.Value = Convert.ToDouble(value);
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private void ApplyFormatting(IXLWorksheet worksheet, int columnCount)
    {
        if (_options.StyleHeader)
        {
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD3, 0xD3, 0xD3);
            headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        if (_options.FreezeHeader)
        {
            worksheet.SheetView.FreezeRows(1);
        }

        if (_options.AutoFilter && columnCount > 0)
        {
            worksheet.Range(1, 1, 1, columnCount).SetAutoFilter();
        }
    }
}
