namespace ApiGateway.GraphQL;

public sealed class Query
{
    public Task<UserResponse> GetUser(
        string id,
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.GetUserAsync(id, ExtractToken(httpContextAccessor), cancellationToken);

    public Task<UsersResponse> GetUsers(
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.GetUsersAsync(ExtractToken(httpContextAccessor), cancellationToken);

    public Task<UserResponse> GetCurrentUser(
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.GetCurrentUserAsync(ExtractToken(httpContextAccessor), cancellationToken);

    internal static string? ExtractToken(IHttpContextAccessor httpContextAccessor)
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        return header is not null && header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? header["Bearer ".Length..]
            : null;
    }
}
