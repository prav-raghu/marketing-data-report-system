namespace IngestionApi.Dtos;

public sealed record FailRunRequestDto
{
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
}
