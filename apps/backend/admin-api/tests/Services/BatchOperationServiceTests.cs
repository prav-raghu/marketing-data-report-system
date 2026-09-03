using AdminApi.Dtos;
using AdminApi.Services;
using AdminApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Types;
using Xunit;

namespace AdminApi.Tests.Services;

public sealed class BatchOperationServiceTests
{
    [Fact]
    public async Task ExecuteBatchAsync_ThrowsInvalidOperationException_WhenBatchSizeExceedsMax()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Custom,
            Items = new[] { new BatchOperationItem<string>("1", "a"), new BatchOperationItem<string>("2", "b") },
            Options = new BatchOperationOptions { MaxBatchSize = 1 },
        };

        var act = () => service.ExecuteBatchAsync(operation, _ => Task.FromResult<object?>(null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Batch size exceeds maximum allowed (1)");
    }

    [Fact]
    public async Task ExecuteBatchAsync_ContinuesProcessing_WhenContinueOnErrorIsTrue()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Custom,
            Items = new[] { new BatchOperationItem<string>("1", "fail"), new BatchOperationItem<string>("2", "ok") },
            Options = new BatchOperationOptions { ContinueOnError = true },
        };

        var summary = await service.ExecuteBatchAsync(operation, item =>
            item == "fail" ? throw new InvalidOperationException("boom") : Task.FromResult<object?>(item));

        summary.Total.Should().Be(2);
        summary.Successful.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.Results.Should().Contain(r => r.Id == "1" && !r.Success && r.Error == "boom");
        summary.Results.Should().Contain(r => r.Id == "2" && r.Success);
    }

    [Fact]
    public async Task ExecuteBatchAsync_StopsAtFirstFailure_WhenContinueOnErrorIsFalse()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Custom,
            Items = new[] { new BatchOperationItem<string>("1", "fail"), new BatchOperationItem<string>("2", "would-succeed") },
        };

        var summary = await service.ExecuteBatchAsync(operation, item =>
            item == "fail" ? throw new InvalidOperationException("boom") : Task.FromResult<object?>(item));

        summary.Successful.Should().Be(0);
        summary.Failed.Should().Be(1);
        summary.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteBatchAsync_Throws_WhenValidateBeforeExecuteAndItemsAreEmpty()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Custom,
            Items = Array.Empty<BatchOperationItem<string>>(),
            Options = new BatchOperationOptions { ValidateBeforeExecute = true },
        };

        var act = () => service.ExecuteBatchAsync(operation, _ => Task.FromResult<object?>(null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Batch operation must contain at least one item");
    }

    [Fact]
    public async Task ExecuteBatchAsync_Throws_WhenValidateBeforeExecuteAndIdsAreDuplicated()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Custom,
            Items = new[] { new BatchOperationItem<string>("dup", "a"), new BatchOperationItem<string>("dup", "b") },
            Options = new BatchOperationOptions { ValidateBeforeExecute = true },
        };

        var act = () => service.ExecuteBatchAsync(operation, _ => Task.FromResult<object?>(null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Batch operation contains duplicate IDs");
    }

    [Fact]
    public async Task BulkCreateUsersAsync_CreatesAllUsers_WhenAllAreValid()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var service = new BatchOperationService(db);
        var users = new[]
        {
            new BulkCreateUserItemDto { Email = "one@test.com", Username = "one", Password = "hashed", IpAddress = "127.0.0.1", RoleId = role.Id, UserStatusId = status.Id },
            new BulkCreateUserItemDto { Email = "two@test.com", Username = "two", Password = "hashed", IpAddress = "127.0.0.1", RoleId = role.Id, UserStatusId = status.Id },
        };

        var summary = await service.BulkCreateUsersAsync(users);

        summary.Successful.Should().Be(2);
        summary.Failed.Should().Be(0);
        (await db.Users.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task BulkCreateUsersAsync_ReportsPartialFailure_WhenTwoItemsShareTheSameEmail()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var service = new BatchOperationService(db);
        var users = new[]
        {
            new BulkCreateUserItemDto { Email = "duplicate@test.com", Username = "first", Password = "hashed", IpAddress = "127.0.0.1", RoleId = role.Id, UserStatusId = status.Id },
            new BulkCreateUserItemDto { Email = "duplicate@test.com", Username = "second", Password = "hashed", IpAddress = "127.0.0.1", RoleId = role.Id, UserStatusId = status.Id },
        };

        var summary = await service.BulkCreateUsersAsync(users);

        summary.Total.Should().Be(2);
        summary.Successful.Should().Be(1);
        summary.Failed.Should().Be(1);
    }

    [Fact]
    public async Task BulkUpdateUserStatusAsync_UpdatesStatus_ForExistingUsers()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var onlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Online");
        var offlineStatus = await UserStatusBuilder.CreateAsync(db, s => s.Name = "Offline");
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = onlineStatus.Id; });
        var service = new BatchOperationService(db);

        var summary = await service.BulkUpdateUserStatusAsync(new[] { new BulkUpdateStatusItemDto { UserId = user.Id, UserStatusId = offlineStatus.Id } });

        summary.Successful.Should().Be(1);
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.UserStatusId.Should().Be(offlineStatus.Id);
    }

    [Fact]
    public async Task BulkDeleteUsersAsync_RemovesUsers_WhenAllExist()
    {
        await using var db = TestDbContextFactory.Create();
        var role = await RoleBuilder.CreateAsync(db);
        var status = await UserStatusBuilder.CreateAsync(db);
        var user = await UserBuilder.CreateAsync(db, u => { u.RoleId = role.Id; u.UserStatusId = status.Id; });
        var service = new BatchOperationService(db);

        var summary = await service.BulkDeleteUsersAsync(new[] { user.Id });

        summary.Successful.Should().Be(1);
        (await db.Users.AnyAsync(u => u.Id == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task BulkDeleteUsersAsync_ThrowsInvalidOperationException_WhenUserIdDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BatchOperationService(db);

        var act = () => service.BulkDeleteUsersAsync(new[] { Guid.NewGuid().ToString() });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
