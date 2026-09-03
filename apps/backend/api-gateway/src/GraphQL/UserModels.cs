namespace ApiGateway.GraphQL;

public sealed record UserType
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public required string Role { get; init; }
    public required bool IsActive { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}

public sealed record UserResponse
{
    public required bool Success { get; init; }
    public UserType? Data { get; init; }
    public string? Error { get; init; }
}

public sealed record UsersResponse
{
    public required bool Success { get; init; }
    public IReadOnlyList<UserType>? Data { get; init; }
    public string? Error { get; init; }
}

public sealed record CreateUserInput(string Email, string Password, string? FirstName, string? LastName, string? Role);

public sealed record UpdateUserInput(string? FirstName, string? LastName, bool? IsActive);
