using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotChocolate;

namespace ApiGateway.GraphQL;

public sealed class UserProxyClient
{
    private readonly HttpClient _httpClient;

    public UserProxyClient(HttpClient httpClient) => _httpClient = httpClient;

    public Task<UserResponse> GetUserAsync(string id, string? token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"/api/v1/users/{id}", null, token, cancellationToken);

    public Task<UsersResponse> GetUsersAsync(string? token, CancellationToken cancellationToken) =>
        SendListAsync("/api/v1/users", token, cancellationToken);

    public Task<UserResponse> GetCurrentUserAsync(string? token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, "/api/v1/users/me", null, token, cancellationToken);

    public Task<UserResponse> CreateUserAsync(CreateUserInput input, string? token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "/api/v1/users", input, token, cancellationToken);

    public Task<UserResponse> UpdateUserAsync(string id, UpdateUserInput input, string? token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"/api/v1/users/{id}", input, token, cancellationToken);

    public Task<UserResponse> DeleteUserAsync(string id, string? token, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, $"/api/v1/users/{id}", null, token, cancellationToken);

    private async Task<UserResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(method, path, body, token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken);
            return result ?? new UserResponse { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage(ex.Message).SetCode("INTERNAL_SERVER_ERROR").Build());
        }
    }

    private async Task<UsersResponse> SendListAsync(string path, string? token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, path, null, token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<UsersResponse>(cancellationToken: cancellationToken);
            return result ?? new UsersResponse { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ErrorBuilder.New().SetMessage(ex.Message).SetCode("INTERNAL_SERVER_ERROR").Build());
        }
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }
}
