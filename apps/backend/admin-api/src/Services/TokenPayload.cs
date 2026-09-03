namespace AdminApi.Services;

public sealed record TokenPayload
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
    public required string Scope { get; init; }
    public string? Jti { get; init; }
    public string? Type { get; init; }
    public long? Iat { get; init; }
}

public sealed record MfaChallengePayload
{
    public required string Id { get; init; }
    public required string Type { get; init; }
}

public sealed record TokenPair(string AccessToken, string RefreshToken, string RefreshTokenId);
