using AdminApi.Dtos;
using AdminApi.Services;
using AdminApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using OtpNet;
using Xunit;

namespace AdminApi.Tests.Services;

public sealed class UserServiceTests
{
    private readonly Mock<IEmailService> _emailService = new();

    public UserServiceTests() =>
        _emailService
            .Setup(e => e.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private UserService BuildService(DotNetMonoRepoTemplate.Database.AppDbContext db) =>
        new(db, _emailService.Object, TestOptions.AdminApi());

    [Fact]
    public async Task GetUserProfileAsync_ReturnsNull_WhenUserIsNotAdminTier()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        var user = await UserBuilder.CreateAsync(db, u => u.RoleId = role.Id);
        var service = BuildService(db);

        var result = await service.GetUserProfileAsync(user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsProfile_WhenUserIsAdminTier()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var user = await UserBuilder.CreateAsync(db, u => u.RoleId = role.Id);
        var service = BuildService(db);

        var result = await service.GetUserProfileAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Username.Should().Be(user.Username);
    }

    [Fact]
    public async Task GetOnlineUsersAsync_FiltersBySearchQuery()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var matching = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Username = "findable-user";
        });
        await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Username = "other-user";
        });
        var service = BuildService(db);

        var result = await service.GetOnlineUsersAsync(new GetUsersPagedRequestDto { Page = 1, PageSize = 10, SearchQuery = "findable" });

        result.Total.Should().Be(1);
        result.Users.Should().ContainSingle(u => u.Id == matching.Id);
    }

    [Fact]
    public async Task GetOnlineUsersCountAsync_CountsOnlyOnlineUsers()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var online = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var offline = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = online.Id; });
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = offline.Id; });
        var service = BuildService(db);

        var count = await service.GetOnlineUsersCountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task OnboardUserAsync_CreatesUserAndSendsEmail_WhenUsernameAndEmailAreAvailable()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var service = BuildService(db);

        var result = await service.OnboardUserAsync(new OnboardingRequestDto
        {
            Username = "new-user",
            Email = "new-user@test.com",
            Password = "Test-password-1",
            AllowEmailCommunications = true,
            IpAddress = "127.0.0.1",
            UserStatusId = status.Id,
            RoleId = role.Id,
        });

        result.IsSuccessful.Should().BeTrue();
        (await db.Users.AnyAsync(u => u.Username == "new-user")).Should().BeTrue();
        _emailService.Verify(
            e => e.SendMailAsync("new-user@test.com", It.IsAny<string>(), "admin-onboarding-notification", It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnboardUserAsync_ReturnsFailure_WhenUsernameAlreadyExists()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var existing = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.OnboardUserAsync(new OnboardingRequestDto
        {
            Username = existing.Username,
            Email = "different@test.com",
            Password = "Test-password-1",
            AllowEmailCommunications = false,
            IpAddress = "127.0.0.1",
            UserStatusId = status.Id,
            RoleId = role.Id,
        });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Username already exists");
    }

    [Fact]
    public async Task OnboardUserAsync_ReturnsFailure_WhenEmailAlreadyExists()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var existing = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.OnboardUserAsync(new OnboardingRequestDto
        {
            Username = "different-username",
            Email = existing.Email,
            Password = "Test-password-1",
            AllowEmailCommunications = false,
            IpAddress = "127.0.0.1",
            UserStatusId = status.Id,
            RoleId = role.Id,
        });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Email already exists");
    }

    [Fact]
    public async Task IsEmailAvailableAsync_ReturnsFalse_WhenEmailIsTaken()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        (await service.IsEmailAvailableAsync(user.Email)).Should().BeFalse();
        (await service.IsEmailAvailableAsync("free-email@test.com")).Should().BeTrue();
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_ReturnsFalse_WhenUsernameIsTaken()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        (await service.IsUsernameAvailableAsync(user.Username)).Should().BeFalse();
        (await service.IsUsernameAvailableAsync("free-username")).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesProfile_WhenUsernameAndEmailAreAvailable()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequestDto { Username = "updated-username", Email = "updated@test.com" });

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Username.Should().Be("updated-username");
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.Email.Should().Be("updated@test.com");
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsFailure_WhenUsernameIsTakenByAnotherUser()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var other = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequestDto { Username = other.Username, Email = user.Email });

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Username already taken");
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangesPassword_WhenCurrentPasswordIsCorrect()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1");
        });
        var service = BuildService(db);

        var result = await service.ChangePasswordAsync(user.Id, "Test-password-1", "New-password-1");

        result.IsSuccessful.Should().BeTrue();
        var reloaded = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("New-password-1", reloaded!.Password).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsFailure_WhenCurrentPasswordIsIncorrect()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u =>
        {
            u.RoleId = role.Id;
            u.UserStatusId = status.Id;
            u.Password = BCrypt.Net.BCrypt.HashPassword("Test-password-1");
        });
        var service = BuildService(db);

        var result = await service.ChangePasswordAsync(user.Id, "wrong-password", "New-password-1");

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Current password is incorrect");
    }

    [Fact]
    public async Task Setup2FAAsync_ReturnsSecretAndQrCode_WhenUserExists()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.Setup2FAAsync(user.Id);

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Secret.Should().NotBeNullOrEmpty();
        result.Data.QrCode.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task Verify2FAAsync_EnablesTwoFactor_WhenCodeIsValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);
        var setup = await service.Setup2FAAsync(user.Id);
        var totp = new Totp(Base32Encoding.ToBytes(setup.Data!.Secret));

        var result = await service.Verify2FAAsync(user.Id, totp.ComputeTotp());

        result.IsSuccessful.Should().BeTrue();
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Verify2FAAsync_ReturnsFailure_WhenCodeIsInvalid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);
        await service.Setup2FAAsync(user.Id);

        var result = await service.Verify2FAAsync(user.Id, "000000");

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("Invalid verification code");
    }

    [Fact]
    public async Task Disable2FAAsync_DisablesTwoFactor_WhenCodeIsValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);
        var setup = await service.Setup2FAAsync(user.Id);
        var totp = new Totp(Base32Encoding.ToBytes(setup.Data!.Secret));
        await service.Verify2FAAsync(user.Id, totp.ComputeTotp());

        var result = await service.Disable2FAAsync(user.Id, totp.ComputeTotp());

        result.IsSuccessful.Should().BeTrue();
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.TwoFactorEnabled.Should().BeFalse();
        reloaded.TwoFactorSecret.Should().BeNull();
    }

    [Fact]
    public async Task Disable2FAAsync_ReturnsFailure_WhenTwoFactorIsNotEnabled()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.Disable2FAAsync(user.Id, "000000");

        result.IsSuccessful.Should().BeFalse();
        result.Message.Should().Be("User not found or 2FA not enabled");
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_ReturnsTrue_ForValidCode()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);
        var setup = await service.Setup2FAAsync(user.Id);
        var totp = new Totp(Base32Encoding.ToBytes(setup.Data!.Secret));

        (await service.VerifyTotpCodeAsync(user.Id, totp.ComputeTotp())).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTotpCodeAsync_ReturnsFalse_WhenUserHasNoTwoFactorSecret()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        (await service.VerifyTotpCodeAsync(user.Id, "000000")).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserDetailsAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = BuildService(db);

        var result = await service.GetUserDetailsAsync(Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserDetailsAsync_ReturnsDetails_WhenUserExists()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = BuildService(db);

        var result = await service.GetUserDetailsAsync(user.Id);

        result.Should().NotBeNull();
        result!.Data!.Id.Should().Be(user.Id);
        result.Data.Roles!.Id.Should().Be(role.Id);
        result.Data.Status!.Id.Should().Be(status.Id);
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsAllRoles()
    {
        await using var db = TestDbContextFactory.Create();
        await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.Support);
        var service = BuildService(db);

        var result = await service.GetUserRolesAsync();

        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserStatusesAsync_ReturnsAllStatuses()
    {
        await using var db = TestDbContextFactory.Create();
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        var service = BuildService(db);

        var result = await service.GetUserStatusesAsync();

        result.Data.Should().HaveCount(2);
    }
}
