using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Dtos;

public sealed record RegisterRequestDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Email { get; init; }
    public required int Age { get; init; }
    public required string GenderId { get; init; }
    public required bool AcceptTermsAndConditions { get; init; }
    public bool AllowEmailCommunications { get; init; }
    public string Ip { get; set; } = string.Empty;
}

public sealed record LoginRequestDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool RememberMe { get; init; }
}

public sealed record RefreshTokenRequestDto
{
    public required string RefreshToken { get; init; }
    public bool RememberMe { get; init; }
}

public sealed record LoginResponseData
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
}

public sealed record LoginResponseDto : ResponseDto
{
    public LoginResponseData? Data { get; init; }
}

public sealed record RegisterResponseData
{
    public required string Email { get; init; }
}

public sealed record RegisterResponseDto : ResponseDto
{
    public RegisterResponseData? Data { get; init; }
}

public sealed record VerifyEmailResponseDto : ResponseDto;

public sealed record RefreshResponseData
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}

public sealed record LogoutRequestDto
{
    public string? RefreshToken { get; init; }
}

public sealed record ResendVerificationEmailDto
{
    public required string Email { get; init; }
}
