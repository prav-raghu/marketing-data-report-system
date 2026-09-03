namespace AdminApi.Dtos;

public sealed record BulkCreateUserItemDto
{
    public required string Email { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string IpAddress { get; init; }
    public required string RoleId { get; init; }
    public required string UserStatusId { get; init; }
}

public sealed record BulkCreateUsersDto
{
    public required IReadOnlyList<BulkCreateUserItemDto> Users { get; init; }
}

public sealed record BulkUpdateStatusItemDto
{
    public required string UserId { get; init; }
    public required string UserStatusId { get; init; }
}

public sealed record BulkUpdateStatusDto
{
    public required IReadOnlyList<BulkUpdateStatusItemDto> Updates { get; init; }
}

public sealed record BulkDeleteUsersDto
{
    public required IReadOnlyList<string> UserIds { get; init; }
}

public sealed record CustomBatchItemDto
{
    public required string Id { get; init; }
    public required IReadOnlyDictionary<string, object?> Data { get; init; }
}

public sealed record CustomBatchDto
{
    public required string Operation { get; init; }
    public required IReadOnlyList<CustomBatchItemDto> Items { get; init; }
}
