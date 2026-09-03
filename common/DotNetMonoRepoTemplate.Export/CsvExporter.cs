using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace DotNetMonoRepoTemplate.Export;

public sealed record CsvExportOptions
{
    public IReadOnlyList<string>? Headers { get; init; }
    public string Delimiter { get; init; } = ",";
    public bool Bom { get; init; }
}

public sealed class CsvExporter
{
    private readonly CsvExportOptions _options;

    public CsvExporter(CsvExportOptions? options = null) => _options = options ?? new CsvExportOptions();

    public async Task<byte[]> ExportToBufferAsync<T>(IReadOnlyList<T> data, CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        using var memoryStream = new MemoryStream();
        await WriteToStreamAsync(memoryStream, data, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task WriteToStreamAsync<T>(
        Stream stream,
        IReadOnlyList<T> data,
        CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        if (_options.Bom)
        {
            var preamble = Encoding.UTF8.GetPreamble();
            await stream.WriteAsync(preamble, cancellationToken);
        }

        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = _options.Delimiter });

        var columns = _options.Headers ?? (data.Count > 0 ? data[0].Keys.ToList() : new List<string>());
        WriteHeader(csv, columns);
        await csv.NextRecordAsync();

        foreach (var record in data)
        {
            WriteRow(csv, columns, record);
            await csv.NextRecordAsync();
        }
    }

    public async Task StreamAsync<T>(
        Stream stream,
        IAsyncEnumerable<T> dataSource,
        CancellationToken cancellationToken = default)
        where T : IReadOnlyDictionary<string, object?>
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = _options.Delimiter });

        var headerWritten = false;
        var columns = _options.Headers;

        await foreach (var record in dataSource.WithCancellation(cancellationToken))
        {
            columns ??= record.Keys.ToList();
            if (!headerWritten)
            {
                WriteHeader(csv, columns);
                await csv.NextRecordAsync();
                headerWritten = true;
            }

            WriteRow(csv, columns, record);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static void WriteHeader(CsvWriter csv, IReadOnlyList<string> columns)
    {
        foreach (var column in columns)
        {
            csv.WriteField(column);
        }
    }

    private static void WriteRow<T>(CsvWriter csv, IReadOnlyList<string> columns, T record)
        where T : IReadOnlyDictionary<string, object?>
    {
        foreach (var column in columns)
        {
            csv.WriteField(record.TryGetValue(column, out var value) ? value : null);
        }
    }
}
