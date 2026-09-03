using System.Net.Http.Json;
using System.Text.Json;
using AdminWeb.Auth;
using AdminWeb.Models;

namespace AdminWeb.Services;

public sealed class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenStore _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions;

    public string? PendingMfaToken { get; set; }

    public AuthService(IHttpClientFactory httpClientFactory, AuthTokenStore tokenStore, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _tokenStore = tokenStore;
        _jsonOptions = jsonOptions;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request, _jsonOptions, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions, cancellationToken);

        if (result is { IsSuccessful: true, Data: not null })
        {
            if (result.Data.MfaRequired == true)
            {
                PendingMfaToken = result.Data.MfaToken;
            }
            else
            {
                _tokenStore.SetTokens(result.Data.AuthToken, result.Data.RefreshToken, result.Data.Username);
            }
        }

        return result;
    }

    public async Task<LoginResponse?> VerifyLoginMfaAsync(VerifyLoginMfaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/verify-login-mfa", request, _jsonOptions, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions, cancellationToken);

        if (result is { IsSuccessful: true, Data: not null })
        {
            PendingMfaToken = null;
            _tokenStore.SetTokens(result.Data.AuthToken, result.Data.RefreshToken, result.Data.Username);
        }

        return result;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _httpClient.GetAsync("api/v1/auth/logout", cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
        finally
        {
            _tokenStore.Clear();
        }
    }

    public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<UserProfile>("api/v1/auth/me", _jsonOptions, cancellationToken);
}
