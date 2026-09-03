using CustomerApi.Services;
using CustomerApi.Tests.Fixtures;
using FluentAssertions;
using DotNetMonoRepoTemplate.Types;
using Xunit;

namespace CustomerApi.Tests.Services;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetUsersAsync_ExcludesInactiveUsers()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.IsActive = true; });
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.IsActive = false; });
        var service = new UserService(db);

        var result = await service.GetUsersAsync(new UserFilters(), loggedInUserId: null);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsersAsync_ExcludesLoggedInUser()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var self = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var other = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new UserService(db);

        var result = await service.GetUsersAsync(new UserFilters(), loggedInUserId: self.Id);

        result.Should().ContainSingle(u => u.Id == other.Id);
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByGenderAndAgeRange()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var match = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.GenderId = "female"; u.Age = 30; });
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.GenderId = "male"; u.Age = 30; });
        await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.GenderId = "female"; u.Age = 60; });
        var service = new UserService(db);

        var result = await service.GetUsersAsync(
            new UserFilters { GenderId = "female", MinAge = 25, MaxAge = 40 },
            loggedInUserId: null);

        result.Should().ContainSingle(u => u.Id == match.Id);
    }

    [Fact]
    public async Task GetUsersAsync_RespectsLimitAndOffset()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        for (var i = 0; i < 5; i++)
        {
            await UserBuilder.CreateAsync(db, u =>
            {
                u.RoleId = role.Id;
                u.UserStatusId = status.Id;
                u.LastSeen = DateTime.UtcNow.AddMinutes(-i);
            });
        }
        var service = new UserService(db);

        var result = await service.GetUsersAsync(new UserFilters { Limit = 2, Offset = 1 }, loggedInUserId: null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenUserNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new UserService(db);

        var result = await service.GetUserByIdAsync("missing-id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenUserIsInactive()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; u.IsActive = false; });
        var service = new UserService(db);

        var result = await service.GetUserByIdAsync(user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsSummary_WhenUserFoundAndActive()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new UserService(db);

        var result = await service.GetUserByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Username.Should().Be(user.Username);
    }

    [Fact]
    public async Task GetAuthorizedUserByIdAsync_ReturnsNull_WhenUserIsNotChatUserRole()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.SuperAdmin);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new UserService(db);

        var result = await service.GetAuthorizedUserByIdAsync(user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthorizedUserByIdAsync_ReturnsAuthorizedUser_WhenUserIsChatUserRole()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db, r => r.Name = RoleName.ChatUser);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new UserService(db);

        var result = await service.GetAuthorizedUserByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Role.Should().Be(RoleName.ChatUser);
    }
}
