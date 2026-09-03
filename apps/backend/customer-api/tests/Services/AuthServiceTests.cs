using CustomerApi.Dtos;
using CustomerApi.Services;
using CustomerApi.Tests.Fixtures;
using FluentAssertions;
using Moq;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using StackExchange.Redis;
using Xunit;

namespace CustomerApi.Tests.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<IEmailService> _emailService = new();

    private AuthService CreateService(
        DotNetMonoRepoTemplate.Database.AppDbContext db,
        out Mock<IConnectionMultiplexer> multiplexer,
        out Mock<IDatabase> database,
        bool redisConnected = false)
    {
        database = new Mock<IDatabase>();
        multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.SetupGet(m => m.IsConnected).Returns(redisConnected);
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var options = TestOptions.CustomerApi();
        var tokenService = new TokenService(options, multiplexer.Object);
        return new AuthService(db, tokenService, _emailService.Object, multiplexer.Object, options);
    }

    private static RegisterRequestDto ValidRegisterRequest(string? username = null, string? email = null) => new()
    {
        Username = username ?? "Valid Username",
        Password = "Test-password-1",
        Email = email ?? $"user-{Guid.NewGuid():N}@test.com",
        Age = 25,
        GenderId = "gender-unspecified",
        AcceptTermsAndConditions = true,
        AllowEmailCommunications = false,
        Ip = "127.0.0.1",
    };

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenTermsNotAccepted()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest() with { AcceptTermsAndConditions = false };

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("terms and conditions");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenUsernameHasInvalidCharacters()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest(username: "invalid123");

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("invalid characters");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenUsernameAlreadyTaken()
    {
        await using var db = TestDbContextFactory.Create();
        await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var existingUser = await UserBuilder.CreateAsync(db, u => u.Username = "Taken Username");
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest(username: existingUser.Username);

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("already taken");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenEmailDomainIsProhibited()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest(email: "attacker@mailinator.com");

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("not allowed");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenEmailAlreadyRegistered()
    {
        await using var db = TestDbContextFactory.Create();
        await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var existingUser = await UserBuilder.CreateAsync(db);
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest(email: existingUser.Email);

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("already registered");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenRoleOrStatusMissing()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest();

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("registration failed");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsFailure_WhenVerificationEmailFailsToSend()
    {
        await using var db = TestDbContextFactory.Create();
        await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        _emailService
            .Setup(e => e.SendMailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest();

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("verification email");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsSuccess_WhenRegistrationValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        _emailService
            .Setup(e => e.SendMailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(db, out _, out _);
        var request = ValidRegisterRequest();

        var result = await service.RegisterAsync(request);

        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be(request.Email);
        var persistedUser = db.Users.Single(u => u.Email == request.Email);
        persistedUser.RoleId.Should().Be(role.Id);
        persistedUser.UserStatusId.Should().Be(status.Id);
        persistedUser.AuthHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify(request.Password, persistedUser.Password).Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_ReturnsFailure_WhenUserNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);

        var result = await service.LoginAsync(new LoginRequestDto { Username = "ghost", Password = "irrelevant" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_ReturnsFailure_WhenPasswordIncorrect()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
        });
        var service = CreateService(db, out _, out _);

        var result = await service.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "wrong-password" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_ReturnsFailure_WhenAccountLockedDueToTooManyFailedAttempts()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out var database, redisConnected: true);
        database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync((RedisValue)"5");

        var result = await service.LoginAsync(new LoginRequestDto { Username = "someone", Password = "irrelevant" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("temporarily locked");
    }

    [Fact]
    public async Task LoginAsync_IncrementsFailedAttemptCounter_WhenPasswordIncorrectAndRedisConnected()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
        });
        var service = CreateService(db, out _, out var database, redisConnected: true);
        database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
        database.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>())).ReturnsAsync(1L);

        var result = await service.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "wrong-password" });

        result.IsSuccessful.Should().BeFalse();
        database.Verify(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ReturnsFailure_WhenUserPendingVerification()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1");
        });
        var service = CreateService(db, out _, out _);

        var result = await service.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "Test-password-1" });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("verify your email");
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenCredentialsValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = onlineStatus.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1");
        });
        var service = CreateService(db, out _, out _);

        var result = await service.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "Test-password-1" });

        result.IsSuccessful.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
        result.Data.RefreshToken.Should().NotBeNullOrEmpty();
        result.Data.UserName.Should().Be(user.Username);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenTokenIsGarbage()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);

        var result = await service.RefreshTokenAsync("not-a-real-token", rememberMe: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_MarksUserOffline_WhenUserExists()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var offlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = onlineStatus.Id;
        });
        var service = CreateService(db, out _, out _);

        await service.LogoutAsync(user.Id, accessToken: null, refreshToken: null);

        var updatedUser = await db.Users.FindAsync(user.Id);
        updatedUser!.UserStatusId.Should().Be(offlineStatus.Id);
    }

    [Fact]
    public async Task LogoutAsync_DoesNotThrow_WhenUserDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);

        var act = async () => await service.LogoutAsync("ghost-user-id", accessToken: null, refreshToken: null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerifyEmailAsync_ReturnsFailure_WhenTokenInvalid()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);

        var result = await service.VerifyEmailAsync("invalid-token");

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task VerifyEmailAsync_ReturnsFailure_WhenAlreadyVerified()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Verified");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.AuthHash = "verify-token";
            u.AuthHashExpiration = DateTime.UtcNow.AddHours(2);
        });
        var service = CreateService(db, out _, out _);

        var result = await service.VerifyEmailAsync(user.AuthHash!);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("already verified");
    }

    [Fact]
    public async Task VerifyEmailAsync_ReturnsFailure_WhenTokenExpired()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.AuthHash = "verify-token";
            u.AuthHashExpiration = DateTime.UtcNow.AddHours(-1);
        });
        var service = CreateService(db, out _, out _);

        var result = await service.VerifyEmailAsync(user.AuthHash!);

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task VerifyEmailAsync_ReturnsSuccess_WhenTokenValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var pendingStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Verified");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = pendingStatus.Id;
            u.AuthHash = "verify-token";
            u.AuthHashExpiration = DateTime.UtcNow.AddHours(2);
        });
        var service = CreateService(db, out _, out _);

        var result = await service.VerifyEmailAsync(user.AuthHash!);

        result.IsSuccessful.Should().BeTrue();
        var updatedUser = await db.Users.FindAsync(user.Id);
        updatedUser!.AuthHash.Should().BeNull();
        updatedUser.AuthHashExpiration.Should().BeNull();
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_ReturnsNeutralMessage_WhenUserNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var service = CreateService(db, out _, out _);

        var result = await service.ResendVerificationEmailAsync("ghost@test.com");

        result.IsSuccessful.Should().BeTrue();
        result.Message.Should().Contain("If that email is registered");
        _emailService.Verify(
            e => e.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_ReturnsNeutralMessage_WhenAlreadyVerified()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Verified");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
        });
        var service = CreateService(db, out _, out _);

        var result = await service.ResendVerificationEmailAsync(user.Email);

        result.IsSuccessful.Should().BeTrue();
        _emailService.Verify(
            e => e.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_SendsNewVerificationEmail_WhenUserUnverified()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Pending Verification");
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.AuthHash = null;
        });
        _emailService
            .Setup(e => e.SendMailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(db, out _, out _);

        var result = await service.ResendVerificationEmailAsync(user.Email);

        result.IsSuccessful.Should().BeTrue();
        var updatedUser = await db.Users.FindAsync(user.Id);
        updatedUser!.AuthHash.Should().NotBeNullOrEmpty();
        _emailService.Verify(
            e => e.SendMailAsync(
                user.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
