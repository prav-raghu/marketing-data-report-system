using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Dtos;

public sealed record LoginRequestDto
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required bool RememberMe { get; init; }
    public string? Ip { get; set; }
}

public sealed record LoginResponseData
{
    public string AuthToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public bool? MfaRequired { get; init; }
    public string? MfaToken { get; init; }
}

public sealed record LoginResponseDto : ResponseDto
{
    public LoginResponseData Data { get; init; } = new();
}

public sealed record VerifyLoginMfaRequestDto
{
    public required string MfaToken { get; init; }
    public required string Code { get; init; }
    public required bool RememberMe { get; init; }
    public string? Ip { get; set; }
}

public sealed record RefreshTokenRequestDto
{
    public required string RefreshToken { get; init; }
    public required bool RememberMe { get; init; }
}

public sealed record ForgotPasswordRequestDto
{
    public required string Email { get; init; }
}

public sealed record ResetPasswordRequestDto
{
    public required string Token { get; init; }
    public required string Password { get; init; }
    public string? ConfirmPassword { get; init; }
}

public sealed record LogOutResponseDto : ResponseDto;

public sealed record ForgotPasswordResponseDto : ResponseDto;

public sealed record ResetPasswordResponseDto : ResponseDto;
