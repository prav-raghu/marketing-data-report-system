using System.IO.Compression;
using System.Text;
using DotNetMonoRepoTemplate.Ingestion.Connectors;
using DotNetMonoRepoTemplate.Ingestion.Envelope;
using DotNetMonoRepoTemplate.Ingestion.Keys;
using DotNetMonoRepoTemplate.Ingestion.Lake;

namespace DotNetMonoRepoTemplate.Ingestion.Writing;

public sealed class EnvelopeWriter : IEnvelopeWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IRawZoneWriter _rawZoneWriter;
    private readonly EnvelopeWriterOptions _options;
    private readonly TimeProvider _timeProvider;

    public EnvelopeWriter(IRawZoneWriter rawZoneWriter, EnvelopeWriterOptions options, TimeProvider timeProvider)
    {
        _rawZoneWriter = rawZoneWriter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<EnvelopeWriteResult> WriteAsync(
        ExtractionRequest request,
        IAsyncEnumerable<SourceRecord> records,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(records);

        var ingestDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var buffer = new List<string>();
        var bufferedBytes = 0L;
        var recordCount = 0;
        var partSequence = 0;
        var compressedBytes = 0L;
        var paths = new List<string>();

        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var line = EnvelopeSerializer.Serialize(BuildEnvelope(request, record, partSequence));
            buffer.Add(line);
            bufferedBytes += Utf8WithoutBom.GetByteCount(line);
            recordCount++;

            if (buffer.Count < _options.MaxRecordsPerPart && bufferedBytes < _options.MaxUncompressedBytesPerPart)
            {
                continue;
            }

            var path = RawZonePath.ForPart(request.SourceSystem, request.SourceEntity, ingestDate, request.RunId, partSequence);
            compressedBytes += await FlushAsync(buffer, path, cancellationToken).ConfigureAwait(false);
            paths.Add(path);
            partSequence++;
            buffer.Clear();
            bufferedBytes = 0;
        }

        if (buffer.Count > 0)
        {
            var path = RawZonePath.ForPart(request.SourceSystem, request.SourceEntity, ingestDate, request.RunId, partSequence);
            compressedBytes += await FlushAsync(buffer, path, cancellationToken).ConfigureAwait(false);
            paths.Add(path);
        }

        return new EnvelopeWriteResult
        {
            RecordCount = recordCount,
            PartCount = paths.Count,
            CompressedBytes = compressedBytes,
            Paths = paths,
        };
    }

    private IngestionEnvelope BuildEnvelope(ExtractionRequest request, SourceRecord record, int batchSequence)
    {
        return new IngestionEnvelope
        {
            EnvelopeVersion = EnvelopeConstants.CurrentVersion,
            SourceSystem = request.SourceSystem,
            SourceEntity = request.SourceEntity,
            ContractVersion = request.ContractVersion,
            RunId = request.RunId,
            BatchSequence = batchSequence,
            IdempotencyKey = record.IdempotencyKey,
            ExtractedAtUtc = record.ExtractedAtUtc,
            IngestedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            SourceWatermarkUtc = record.SourceWatermarkUtc,
            PayloadFormat = record.PayloadFormat,
            PayloadHash = PayloadHasher.ComputeHash(record.Payload),
            Payload = record.Payload,
            RawArtifactPath = record.RawArtifactPath,
        };
    }

    private async Task<long> FlushAsync(IReadOnlyList<string> lines, string path, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();

        await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        await using (var writer = new StreamWriter(gzip, Utf8WithoutBom) { NewLine = "\n" })
        {
            foreach (var line in lines)
            {
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        var payload = output.ToArray();
        await _rawZoneWriter.WriteAsync(path, payload, cancellationToken).ConfigureAwait(false);
        return payload.LongLength;
    }
}
