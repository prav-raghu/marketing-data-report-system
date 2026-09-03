namespace AdminWeb.Models;

public sealed record Setup2FAData
{
    public string Secret { get; init; } = string.Empty;
    public string QrCode { get; init; } = string.Empty;
}

public sealed record Setup2FAResponse
{
    public bool IsSuccessful { get; init; }
    public string? Message { get; init; }
    public Setup2FAData? Data { get; init; }
}

public sealed class Verify2FARequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class Disable2FARequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed record TwoFactorActionResponse
{
    public bool IsSuccessful { get; init; }
    public string? Message { get; init; }
}
