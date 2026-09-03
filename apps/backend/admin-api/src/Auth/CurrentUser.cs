namespace AdminApi.Auth;

public sealed record CurrentUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
    public required string Scope { get; init; }
}
