using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.GraphQL;
using ApiGateway.Tests.Fixtures;
using FluentAssertions;
using HotChocolate;
using Xunit;

namespace ApiGateway.Tests.GraphQL;

public sealed class UserProxyClientTests
{
    private static UserType BuildUser() => new()
    {
        Id = "user-1",
        Email = "user@test.com",
        FirstName = "Jane",
        LastName = "Doe",
        Role = "customer",
        IsActive = true,
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };

    [Fact]
    public async Task GetUserAsync_SendsGetRequestToUserPath_WithBearerToken_WhenTokenProvided()
    {
        var user = BuildUser();
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = user }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        var result = await client.GetUserAsync("user-1", "token-abc", CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/users/user-1");
        handler.LastRequest.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("token-abc");
        result.Success.Should().BeTrue();
        result.Data.Should().Be(user);
    }

    [Fact]
    public async Task GetUserAsync_SendsRequestWithoutAuthorizationHeader_WhenTokenIsNull()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        await client.GetUserAsync("user-1", null, CancellationToken.None);

        handler.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_SendsRequestWithoutAuthorizationHeader_WhenTokenIsEmpty()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        await client.GetUserAsync("user-1", string.Empty, CancellationToken.None);

        handler.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_SendsGetRequestToUsersPath()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UsersResponse { Success = true, Data = new[] { BuildUser() } }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        var result = await client.GetUsersAsync("token-abc", CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/users");
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCurrentUserAsync_SendsGetRequestToMePath()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        await client.GetCurrentUserAsync("token-abc", CancellationToken.None);

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v1/users/me");
    }

    [Fact]
    public async Task CreateUserAsync_SendsPostRequestWithJsonBody()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));
        var input = new CreateUserInput("new@test.com", "Pass-1234", "New", "User", "customer");

        await client.CreateUserAsync(input, "token-abc", CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/users");
        handler.LastRequestBody.Should().Contain("new@test.com");
    }

    [Fact]
    public async Task UpdateUserAsync_SendsPutRequestToUserPath_WithJsonBody()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));
        var input = new UpdateUserInput("Jane", "Smith", true);

        await client.UpdateUserAsync("user-1", input, "token-abc", CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/users/user-1");
        handler.LastRequestBody.Should().Contain("Smith");
    }

    [Fact]
    public async Task DeleteUserAsync_SendsDeleteRequestToUserPath_WithNoBody()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(new UserResponse { Success = true, Data = BuildUser() }));
        var client = new UserProxyClient(CreateHttpClient(handler));

        await client.DeleteUserAsync("user-1", "token-abc", CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/v1/users/user-1");
        handler.LastRequestBody.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsEmptyResponseFailure_WhenResponseBodyDeserializesToNull()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        });
        var client = new UserProxyClient(CreateHttpClient(handler));

        var result = await client.GetUserAsync("missing-id", "token-abc", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Empty response");
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsEmptyResponseFailure_WhenResponseBodyDeserializesToNull()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        });
        var client = new UserProxyClient(CreateHttpClient(handler));

        var result = await client.GetUsersAsync("token-abc", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Empty response");
    }

    [Fact]
    public async Task GetUserAsync_ThrowsGraphQLException_WhenUnderlyingRequestFails()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var client = new UserProxyClient(CreateHttpClient(handler));

        var act = async () => await client.GetUserAsync("user-1", "token-abc", CancellationToken.None);

        await act.Should().ThrowAsync<GraphQLException>();
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://gateway-under-test.local") };

    private static HttpResponseMessage JsonResponse<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
