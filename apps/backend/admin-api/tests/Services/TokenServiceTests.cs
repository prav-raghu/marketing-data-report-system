using AdminApi.Services;
using AdminApi.Tests.Fixtures;
using FluentAssertions;
using DotNetMonoRepoTemplate.Types;
using Xunit;

namespace AdminApi.Tests.Services;

public sealed class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ReturnsTokensThatVerifySuccessfully_WhenRedisIsDisconnected()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);
        var user = UserBuilder.Build();

        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);

        var accessPayload = service.VerifyAccessToken(tokens.AccessToken);
        var refreshPayload = service.VerifyRefreshToken(tokens.RefreshToken);

        accessPayload.Should().NotBeNull();
        accessPayload!.Id.Should().Be(user.Id);
        accessPayload.Username.Should().Be(user.Username);
        accessPayload.Role.Should().Be(RoleName.SuperAdmin);
        accessPayload.Type.Should().Be("access");
        accessPayload.Permissions.Should().Contain(PermissionName.UserRead);

        refreshPayload.Should().NotBeNull();
        refreshPayload!.Type.Should().Be("refresh");
        refreshPayload.Id.Should().Be(user.Id);
    }

    [Fact]
    public void VerifyAccessToken_ReturnsNull_WhenTokenWasSignedWithRefreshKey()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);
        var user = UserBuilder.Build();

        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);

        service.VerifyAccessToken(tokens.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void VerifyRefreshToken_ReturnsNull_WhenTokenWasSignedWithAccessKey()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);
        var user = UserBuilder.Build();

        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);

        service.VerifyRefreshToken(tokens.AccessToken).Should().BeNull();
    }

    [Fact]
    public void GenerateMfaChallengeToken_VerifyMfaChallengeToken_RoundTripsSuccessfully()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);
        var userId = Guid.NewGuid().ToString();

        var challengeToken = service.GenerateMfaChallengeToken(userId);
        var payload = service.VerifyMfaChallengeToken(challengeToken);

        payload.Should().NotBeNull();
        payload!.Id.Should().Be(userId);
        payload.Type.Should().Be("mfa_challenge");
    }

    [Fact]
    public void VerifyMfaChallengeToken_ReturnsNull_WhenTokenIsARealAccessToken()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);
        var user = UserBuilder.Build();

        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);

        service.VerifyMfaChallengeToken(tokens.AccessToken).Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAllAccessTokensAsync_ThenIsSessionInvalidatedAsync_ReturnsTrue_ForTokenIssuedBeforeInvalidation()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var userId = Guid.NewGuid().ToString();

        await service.InvalidateAllAccessTokensAsync(userId);
        var issuedAtBeforeInvalidation = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

        var invalidated = await service.IsSessionInvalidatedAsync(userId, issuedAtBeforeInvalidation);

        invalidated.Should().BeTrue();
    }

    [Fact]
    public async Task IsSessionInvalidatedAsync_ReturnsFalse_ForTokenIssuedAfterInvalidation()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var userId = Guid.NewGuid().ToString();

        await service.InvalidateAllAccessTokensAsync(userId);
        var issuedAtAfterInvalidation = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();

        var invalidated = await service.IsSessionInvalidatedAsync(userId, issuedAtAfterInvalidation);

        invalidated.Should().BeFalse();
    }

    [Fact]
    public async Task IsSessionInvalidatedAsync_ReturnsFalse_WhenNoMinIatHasBeenRecorded()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);

        var invalidated = await service.IsSessionInvalidatedAsync(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        invalidated.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenBlacklistedAsync_ReturnsFalse_WhenRedisIsDisconnected()
    {
        var redis = RedisTestDouble.Disconnected();
        var service = new TokenService(TestOptions.AdminApi(), redis.Object);

        var blacklisted = await service.IsTokenBlacklistedAsync(Guid.NewGuid().ToString());

        blacklisted.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_BlacklistsTheGivenAccessToken_SoItIsRejectedAfterwards()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var user = UserBuilder.Build();
        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);
        var accessPayload = service.VerifyAccessToken(tokens.AccessToken)!;

        await service.LogoutAsync(user.Id, tokens.AccessToken, null);

        var blacklisted = await service.IsTokenBlacklistedAsync(accessPayload.Jti!);
        blacklisted.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNewTokenPair_WhenRefreshTokenWasStored()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var user = UserBuilder.Build();
        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);
        redis.Seed($"token:refresh:{user.Id}:{tokens.RefreshTokenId}", "seeded");

        var refreshed = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        refreshed.Should().NotBeNull();
        refreshed!.AccessToken.Should().NotBeNullOrEmpty();
        refreshed.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenRefreshTokenWasNeverStored()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var user = UserBuilder.Build();
        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);

        var refreshed = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        refreshed.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenTokenIsBlacklisted()
    {
        var redis = new ConnectedRedisTestDouble();
        var service = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var user = UserBuilder.Build();
        var tokens = service.GenerateToken(user, rememberMe: false, RoleName.SuperAdmin);
        redis.Seed($"token:refresh:{user.Id}:{tokens.RefreshTokenId}", "seeded");
        redis.Seed($"token:blacklist:{tokens.RefreshTokenId}", "1");

        var refreshed = await service.RefreshTokenAsync(tokens.RefreshToken, rememberMe: false);

        refreshed.Should().BeNull();
    }
}
