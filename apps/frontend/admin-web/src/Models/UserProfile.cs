namespace AdminWeb.Models;

public sealed record UserProfile
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool TwoFactorEnabled { get; init; }
}
