namespace IngestionApi.Dtos;

public sealed record CompleteRunRequestDto
{
    public required int RecordCount { get; init; }
    public required int PartCount { get; init; }
    public required long CompressedBytes { get; init; }
}
