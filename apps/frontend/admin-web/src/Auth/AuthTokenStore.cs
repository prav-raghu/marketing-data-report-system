namespace AdminWeb.Auth;

public sealed class AuthTokenStore
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? Username { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public event Action? Changed;

    public void SetTokens(string accessToken, string refreshToken, string username)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Username = username;
        Changed?.Invoke();
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        Username = null;
        Changed?.Invoke();
    }
}
