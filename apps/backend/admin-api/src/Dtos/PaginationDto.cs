namespace AdminApi.Dtos;

public sealed record PaginationRequestDto
{
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public string? Search { get; init; }
    public string? OrderBy { get; init; }
    public string? OrderDir { get; init; }
}

public sealed record PaginationResponseDto<T>
{
    public required IReadOnlyList<T> Data { get; init; }
    public required int Total { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
}
