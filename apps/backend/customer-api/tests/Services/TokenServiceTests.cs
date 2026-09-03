using CustomerApi.Services;
using CustomerApi.Tests.Fixtures;
using FluentAssertions;
using Moq;
using DotNetMonoRepoTemplate.Types;
using StackExchange.Redis;
using Xunit;

namespace CustomerApi.Tests.Services;

public sealed class TokenServiceTests
{
    private static TokenService CreateService(out Mock<IConnectionMultiplexer> multiplexer, out Mock<IDatabase> database, bool redisConnected = true)
    {
        database = new Mock<IDatabase>();
        multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(m => m.IsConnected).Returns(redisConnected);
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return new TokenService(TestOptions.CustomerApi(), multiplexer.Object);
    }

    [Fact]
    public void GenerateToken_ReturnsTokenPair_WithVerifiableAccessAndRefreshTokens()
    {
        var service = CreateService(out _, out _, redisConnected: false);

        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);

        var accessPayload = service.VerifyAccessToken(tokens.AccessToken);
        var refreshPayload = service.VerifyRefreshToken(tokens.RefreshToken);

        accessPayload.Should().NotBeNull();
        accessPayload!.Id.Should().Be("user-1");
        accessPayload.Username.Should().Be("alice");
        accessPayload.Role.Should().Be(RoleName.ChatUser);
        accessPayload.Scope.Should().Be(TokenScope.Customer);
        refreshPayload.Should().NotBeNull();
        refreshPayload!.Id.Should().Be("user-1");
        tokens.RefreshTokenId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerifyAccessToken_ReturnsNull_WhenTokenIsGarbage()
    {
        var service = CreateService(out _, out _, redisConnected: false);

        var payload = service.VerifyAccessToken("not-a-real-token");

        payload.Should().BeNull();
    }

    [Fact]
    public void VerifyRefreshToken_ReturnsNull_WhenGivenAnAccessToken()
    {
        var service = CreateService(out _, out _, redisConnected: false);
        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);

        var payload = service.VerifyRefreshToken(tokens.AccessToken);

        payload.Should().BeNull();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_ReturnsFalse_WhenRedisNotConnected()
    {
        var service = CreateService(out _, out _, redisConnected: false);

        var result = await service.IsTokenBlacklistedAsync("some-jti");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_ReturnsTrue_WhenKeyExistsInRedis()
    {
        var service = CreateService(out _, out var database);
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>())).ReturnsAsync(true);

        var result = await service.IsTokenBlacklistedAsync("some-jti");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenTokenIsGarbage()
    {
        var service = CreateService(out _, out _);

        var result = await service.RefreshTokenAsync("not-a-real-token", rememberMe: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenTokenIsBlacklisted()
    {
        var service = CreateService(out _, out var database);
        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>())).ReturnsAsync(true);

        var result = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenRefreshTokenNotFoundInRedis()
    {
        var service = CreateService(out _, out var database);
        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);
        database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>())).ReturnsAsync(false);

        var result = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsRotatedTokenPair_WhenRefreshTokenValidAndNotBlacklisted()
    {
        var service = CreateService(out _, out var database);
        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);
        database.Setup(d => d.KeyExistsAsync(It.Is<RedisKey>(k => k.ToString().StartsWith("token:blacklist:")))).ReturnsAsync(false);
        database.Setup(d => d.KeyExistsAsync(It.Is<RedisKey>(k => k.ToString().StartsWith("token:refresh:")))).ReturnsAsync(true);

        var result = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBe(tokens.AccessToken);
        result.RefreshToken.Should().NotBe(tokens.RefreshToken);
        var newAccessPayload = service.VerifyAccessToken(result.AccessToken);
        newAccessPayload.Should().NotBeNull();
        newAccessPayload!.Id.Should().Be("user-1");
        database.Verify(
            d => d.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("token:blacklist:")),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_BlacklistsAccessAndRefreshTokens_WhenBothPresent()
    {
        var service = CreateService(out _, out var database);
        var tokens = service.GenerateToken("user-1", "alice", RoleName.ChatUser, rememberMe: false);

        await service.LogoutAsync("user-1", tokens.AccessToken, tokens.RefreshToken);

        database.Verify(
            d => d.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith("token:blacklist:")),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>()),
            Times.Exactly(2));
        database.Verify(d => d.KeyDeleteAsync(It.Is<RedisKey>(k => k.ToString().StartsWith("token:refresh:"))), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_CompletesWithoutError_WhenNoTokensProvided()
    {
        var service = CreateService(out _, out _);

        var act = async () => await service.LogoutAsync("user-1", accessToken: null, refreshToken: null);

        await act.Should().NotThrowAsync();
    }
}
