namespace DotNetMonoRepoTemplate.Types;

public static class Gender
{
    public const string Male = "male";
    public const string Female = "female";
    public const string Other = "other";
}

public sealed record LoginRequest(string Username, string Password);

public sealed record RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Email { get; init; }
    public required int Age { get; init; }
    public required string Gender { get; init; }
    public required string Country { get; init; }
    public required string Region { get; init; }
    public required bool AcceptTermsAndConditions { get; init; }
    public bool? AllowEmailCommunications { get; init; }
}

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LoginResponse(string Token);

public sealed record RegisterResponse : ResponseDto;

public sealed record RefreshTokenResponse(string Token);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record VerifyEmailResponse : ResponseDto;

public sealed record AuthUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public int? Age { get; init; }
    public string? Gender { get; init; }
    public string? Country { get; init; }
    public string? Region { get; init; }
    public bool? IsOnline { get; init; }
    public DateTime? LastSeen { get; init; }
    public bool? IsActive { get; init; }
    public bool? EmailVerified { get; init; }
}

public sealed record AuthState
{
    public AuthUser? User { get; init; }
    public string? Token { get; init; }
    public required bool IsLoading { get; init; }
    public required bool IsAuthenticated { get; init; }
}

public sealed record AuthError
{
    public required string Message { get; init; }
    public string? Code { get; init; }
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}
