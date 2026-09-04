using System.IO.Compression;
using System.Text;
using DotNetMonoRepoTemplate.Ingestion.Lake;

namespace IngestionApi.Tests.Fixtures;

public sealed class InMemoryRawZoneWriter : IRawZoneWriter
{
    public Dictionary<string, byte[]> Written { get; } = new(StringComparer.Ordinal);

    public Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Written[path] = content.ToArray();
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> ReadLines(string path)
    {
        using var input = new MemoryStream(Written[path]);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
