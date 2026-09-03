namespace ApiGateway.GraphQL;

public sealed class Mutation
{
    public Task<UserResponse> CreateUser(
        CreateUserInput input,
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.CreateUserAsync(input, Query.ExtractToken(httpContextAccessor), cancellationToken);

    public Task<UserResponse> UpdateUser(
        string id,
        UpdateUserInput input,
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.UpdateUserAsync(id, input, Query.ExtractToken(httpContextAccessor), cancellationToken);

    public Task<UserResponse> DeleteUser(
        string id,
        [Service] UserProxyClient client,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken) =>
        client.DeleteUserAsync(id, Query.ExtractToken(httpContextAccessor), cancellationToken);
}
