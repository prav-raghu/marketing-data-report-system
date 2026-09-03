using System.Net.Http.Json;
using System.Text.Json;
using AdminWeb.Models;

namespace AdminWeb.Services;

public sealed class UserService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserService(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _jsonOptions = jsonOptions;
    }

    public async Task<Setup2FAResponse?> Setup2FAAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/v1/users/2fa/setup", content: null, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Setup2FAResponse>(_jsonOptions, cancellationToken);
    }

    public async Task<TwoFactorActionResponse?> Verify2FAAsync(Verify2FARequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/users/2fa/verify", request, _jsonOptions, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TwoFactorActionResponse>(_jsonOptions, cancellationToken);
    }

    public async Task<TwoFactorActionResponse?> Disable2FAAsync(Disable2FARequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/users/2fa/disable", request, _jsonOptions, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TwoFactorActionResponse>(_jsonOptions, cancellationToken);
    }
}
