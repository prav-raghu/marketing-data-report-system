namespace DotNetMonoRepoTemplate.Ingestion.Writing;

public sealed record EnvelopeWriterOptions
{
    public int MaxRecordsPerPart { get; init; } = 50_000;
    public long MaxUncompressedBytesPerPart { get; init; } = 64L * 1024 * 1024;
}
