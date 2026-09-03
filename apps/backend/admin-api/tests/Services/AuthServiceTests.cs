using AdminApi.Dtos;
using AdminApi.Services;
using AdminApi.Tests.Fixtures;
using FluentAssertions;
using Moq;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using OtpNet;
using Xunit;

namespace AdminApi.Tests.Services;

public sealed class AuthServiceTests
{
    private const string TestPassword = "Test-password-1";

    private readonly Mock<IEmailService> _emailService = new();

    public AuthServiceTests() =>
        _emailService
            .Setup(e => e.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private AuthService BuildService(DotNetMonoRepoTemplate.Database.AppDbContext db, Mock<StackExchange.Redis.IConnectionMultiplexer>? authRedis = null)
    {
        var tokenService = new TokenService(TestOptions.AdminApi(), RedisTestDouble.Disconnected().Object);
        var userService = new UserService(db, _emailService.Object, TestOptions.AdminApi());
        return new AuthService(db, tokenService, _emailService.Object, userService, (authRedis ?? RedisTestDouble.Disconnected()).Object, TestOptions.AdminApi());
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenCredentialsAreValidAndMfaIsDisabled()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = onlineStatus.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword(TestPassword);
            u.TwoFactorEnabled = false;
        });
        var service = BuildService(db);

        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = TestPassword, RememberMe = false });

        result.IsSuccessful.Should().BeTrue();
        result.Data.AuthToken.Should().NotBeNullOrEmpty();
        result.Data.RefreshToken.Should().NotBeNullOrEmpty();
        result.Data.Username.Should().Be(user.Username);
        result.Data.MfaRequired.Should().NotBe(true);
    }

    [Fact]
    public async Task LoginAsync_ReturnsMfaChallenge_WhenUserHasTwoFactorEnabled()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var pendingStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = pendingStatus.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword(TestPassword);
            u.TwoFactorEnabled = true;
        });
        var service = BuildService(db);

        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = TestPassword, RememberMe = false });

        result.IsSuccessful.Should().BeTrue();
        result.Data.MfaRequired.Should().BeTrue();
        result.Data.MfaToken.Should().NotBeNullOrEmpty();
        result.Data.AuthToken.Should().BeEmpty();

        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.UserStatusId.Should().Be(pendingStatus.Id);
    }

    [Fact]
    public async Task LoginAsync_ReturnsInvalidCredentials_WhenPasswordIsWrong()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword(TestPassword);
        });
        var service = BuildService(db);

        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = "wrong-password", RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_ReturnsInvalidCredentials_WhenUserDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.LoginAsync(new LoginRequestDto { Email = "nobody@test.com", Password = TestPassword, RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_ReturnsInvalidCredentials_WhenUserRoleIsNotAdminTier()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword(TestPassword);
        });
        var service = BuildService(db);

        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = TestPassword, RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_ReturnsAccountLocked_WhenTooManyFailedAttemptsAreRecorded()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword(TestPassword);
        });
        var redis = new ConnectedRedisTestDouble();
        redis.Seed($"login:fail:{user.Email}", "5");
        var service = BuildService(db, redis.Multiplexer);

        var result = await service.LoginAsync(new LoginRequestDto { Email = user.Email, Password = TestPassword, RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Account temporarily locked due to too many failed attempts. Try again later.");
    }

    [Fact]
    public async Task VerifyLoginMfaAsync_ReturnsTokens_WhenChallengeAndCodeAreValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.TwoFactorEnabled = true;
        });
        var userService = new UserService(db, _emailService.Object, TestOptions.AdminApi());
        var setup = await userService.Setup2FAAsync(user.Id);
        var totp = new Totp(Base32Encoding.ToBytes(setup.Data!.Secret));
        var code = totp.ComputeTotp();
        await userService.Verify2FAAsync(user.Id, code);

        var tokenService = new TokenService(TestOptions.AdminApi(), RedisTestDouble.Disconnected().Object);
        var mfaToken = tokenService.GenerateMfaChallengeToken(user.Id);
        var service = new AuthService(db, tokenService, _emailService.Object, userService, RedisTestDouble.Disconnected().Object, TestOptions.AdminApi());

        var result = await service.VerifyLoginMfaAsync(new VerifyLoginMfaRequestDto { MfaToken = mfaToken, Code = code, RememberMe = false });

        result.IsSuccessful.Should().BeTrue();
        result.Data.AuthToken.Should().NotBeNullOrEmpty();
        result.Data.Username.Should().Be(user.Username);
    }

    [Fact]
    public async Task VerifyLoginMfaAsync_ReturnsSessionExpired_WhenMfaTokenIsInvalid()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.VerifyLoginMfaAsync(new VerifyLoginMfaRequestDto { MfaToken = "not-a-real-token", Code = "123456", RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Verification session expired — please log in again");
    }

    [Fact]
    public async Task VerifyLoginMfaAsync_ReturnsInvalidCode_WhenCodeDoesNotMatch()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.TwoFactorEnabled = true;
        });
        var userService = new UserService(db, _emailService.Object, TestOptions.AdminApi());
        await userService.Setup2FAAsync(user.Id);

        var tokenService = new TokenService(TestOptions.AdminApi(), RedisTestDouble.Disconnected().Object);
        var mfaToken = tokenService.GenerateMfaChallengeToken(user.Id);
        var service = new AuthService(db, tokenService, _emailService.Object, userService, RedisTestDouble.Disconnected().Object, TestOptions.AdminApi());

        var result = await service.VerifyLoginMfaAsync(new VerifyLoginMfaRequestDto { MfaToken = mfaToken, Code = "000000", RememberMe = false });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Invalid verification code");
    }

    [Fact]
    public async Task LogoutAsync_SetsUserOfflineAndReturnsSuccess_WhenUserExists()
    {
        await using var db = TestDbContextFactory.Create();
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var offlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = onlineStatus.Id;
        });
        var service = BuildService(db);

        var result = await service.LogoutAsync(user.Id, null, null);

        result.IsSuccessful.Should().BeTrue();
        result.Message.Should().Be("Successfully Logged Out");
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.UserStatusId.Should().Be(offlineStatus.Id);
    }

    [Fact]
    public async Task LogoutAsync_InvalidatesFutureAccessTokens_ViaMinIatMarker()
    {
        await using var db = TestDbContextFactory.Create();
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = onlineStatus.Id;
        });

        var redis = new ConnectedRedisTestDouble();
        var tokenService = new TokenService(TestOptions.AdminApi(), redis.Multiplexer.Object);
        var userService = new UserService(db, _emailService.Object, TestOptions.AdminApi());
        var service = new AuthService(db, tokenService, _emailService.Object, userService, redis.Multiplexer.Object, TestOptions.AdminApi());
        var oldTokenIssuedAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        await service.LogoutAsync(user.Id, null, null);

        var invalidated = await tokenService.IsSessionInvalidatedAsync(user.Id, oldTokenIssuedAt);
        invalidated.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_ReturnsFailure_WhenUserDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.LogoutAsync(Guid.NewGuid().ToString(), null, null);

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsNeutralMessage_WhenEmailDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.ForgotPasswordAsync("nobody@test.com");

        result.IsSuccessful.Should().BeTrue();
        result.Message.Should().Be("If that email exists, a reset link has been sent");
    }

    [Fact]
    public async Task ForgotPasswordAsync_SetsAuthHash_ForAdminTierUser()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
        });
        var service = BuildService(db);

        var result = await service.ForgotPasswordAsync(user.Email);

        result.IsSuccessful.Should().BeTrue();
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.AuthHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPasswordAsync_ResetsPassword_WhenTokenIsValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var resetToken = Guid.NewGuid().ToString();
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.AuthHash = resetToken;
            u.AuthHashExpiration = DateTime.UtcNow.AddHours(1);
        });
        var service = BuildService(db);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = resetToken, Password = "New-password-1", ConfirmPassword = "New-password-1" });

        result.IsSuccessful.Should().BeTrue();
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.AuthHash.Should().BeNull();
        BCrypt.Net.BCrypt.Verify("New-password-1", reloaded.Password).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsFailure_WhenPasswordsDoNotMatch()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = "any-token", Password = "New-password-1", ConfirmPassword = "different" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("New password and confirm password do not match");
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsFailure_WhenTokenIsExpired()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var resetToken = Guid.NewGuid().ToString();
        await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.AuthHash = resetToken;
            u.AuthHashExpiration = DateTime.UtcNow.AddHours(-1);
        });
        var service = BuildService(db);

        var result = await service.ResetPasswordAsync(new ResetPasswordRequestDto { Token = resetToken, Password = "New-password-1", ConfirmPassword = "New-password-1" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Token has expired");
    }
}
