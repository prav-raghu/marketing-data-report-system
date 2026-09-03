namespace AdminWeb.Models;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public sealed record LoginResponseData
{
    public string AuthToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public bool? MfaRequired { get; init; }
    public string? MfaToken { get; init; }
}

public sealed record LoginResponse
{
    public bool IsSuccessful { get; init; }
    public string? Message { get; init; }
    public LoginResponseData? Data { get; init; }
}

public sealed class VerifyLoginMfaRequest
{
    public string MfaToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
