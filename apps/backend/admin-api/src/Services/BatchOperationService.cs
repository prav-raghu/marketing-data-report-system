using AdminApi.Dtos;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Services;

public sealed class BatchOperationService
{
    private readonly AppDbContext _db;

    public BatchOperationService(AppDbContext db) => _db = db;

    public async Task<BatchOperationSummary> ExecuteBatchAsync<T>(
        BatchOperation<T> operation, Func<T, Task<object?>> executor)
    {
        var maxBatchSize = operation.Options?.MaxBatchSize ?? 1000;
        if (operation.Items.Count > maxBatchSize)
        {
            throw new InvalidOperationException($"Batch size exceeds maximum allowed ({maxBatchSize})");
        }

        if (operation.Options?.ValidateBeforeExecute == true)
        {
            ValidateBatch(operation);
        }

        var results = new List<BatchOperationResult<object?>>();
        var successful = 0;
        var failed = 0;

        foreach (var item in operation.Items)
        {
            try
            {
                var result = await executor(item.Data);
                results.Add(new BatchOperationResult<object?> { Id = item.Id, Success = true, Data = result });
                successful++;
            }
            catch (Exception ex)
            {
                results.Add(new BatchOperationResult<object?> { Id = item.Id, Success = false, Error = ex.Message });
                failed++;
                if (operation.Options?.ContinueOnError != true)
                {
                    break;
                }
            }
        }

        return new BatchOperationSummary { Total = operation.Items.Count, Successful = successful, Failed = failed, Results = results };
    }

    public async Task<BatchOperationSummary> ExecuteBatchWithTransactionAsync<T>(
        BatchOperation<T> operation, Func<T, AppDbContext, Task<object?>> executor)
    {
        var maxBatchSize = operation.Options?.MaxBatchSize ?? 1000;
        if (operation.Items.Count > maxBatchSize)
        {
            throw new InvalidOperationException($"Batch size exceeds maximum allowed ({maxBatchSize})");
        }

        if (operation.Options?.ValidateBeforeExecute == true)
        {
            ValidateBatch(operation);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var results = new List<BatchOperationResult<object?>>();
        var successful = 0;
        var failed = 0;

        try
        {
            foreach (var item in operation.Items)
            {
                try
                {
                    var result = await executor(item.Data, _db);
                    results.Add(new BatchOperationResult<object?> { Id = item.Id, Success = true, Data = result });
                    successful++;
                }
                catch (Exception ex)
                {
                    results.Add(new BatchOperationResult<object?> { Id = item.Id, Success = false, Error = ex.Message });
                    failed++;
                    if (operation.Options?.ContinueOnError != true)
                    {
                        throw;
                    }
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException($"Batch transaction failed: {ex.Message}", ex);
        }

        return new BatchOperationSummary { Total = operation.Items.Count, Successful = successful, Failed = failed, Results = results };
    }

    public Task<BatchOperationSummary> BulkCreateUsersAsync(
        IReadOnlyList<BulkCreateUserItemDto> users, CancellationToken cancellationToken = default)
    {
        var operation = new BatchOperation<BulkCreateUserItemDto>
        {
            Type = BatchOperationType.Create,
            Items = users.Select((user, index) => new BatchOperationItem<BulkCreateUserItemDto>($"user-{index}", user)).ToList(),
            Options = new BatchOperationOptions { ContinueOnError = true, ValidateBeforeExecute = true, MaxBatchSize = 500 },
        };

        return ExecuteBatchAsync(operation, async userData =>
        {
            var user = new User
            {
                Email = userData.Email,
                Username = userData.Username,
                Password = userData.Password,
                IpAddress = userData.IpAddress,
                RoleId = userData.RoleId,
                UserStatusId = userData.UserStatusId,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
            return (object?)new { user.Id, user.Email, user.Username };
        });
    }

    public Task<BatchOperationSummary> BulkUpdateUserStatusAsync(
        IReadOnlyList<BulkUpdateStatusItemDto> updates, CancellationToken cancellationToken = default)
    {
        var operation = new BatchOperation<BulkUpdateStatusItemDto>
        {
            Type = BatchOperationType.Update,
            Items = updates.Select(update => new BatchOperationItem<BulkUpdateStatusItemDto>(update.UserId, update)).ToList(),
            Options = new BatchOperationOptions { ContinueOnError = true, ValidateBeforeExecute = false, MaxBatchSize = 1000 },
        };

        return ExecuteBatchWithTransactionAsync(operation, async (updateData, db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == updateData.UserId, cancellationToken)
                ?? throw new InvalidOperationException($"User {updateData.UserId} not found");
            user.UserStatusId = updateData.UserStatusId;
            await db.SaveChangesAsync(cancellationToken);
            return (object?)user.Id;
        });
    }

    public Task<BatchOperationSummary> BulkDeleteUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
    {
        var operation = new BatchOperation<string>
        {
            Type = BatchOperationType.Delete,
            Items = userIds.Select(id => new BatchOperationItem<string>(id, id)).ToList(),
            Options = new BatchOperationOptions { ContinueOnError = false, ValidateBeforeExecute = true, MaxBatchSize = 500 },
        };

        return ExecuteBatchWithTransactionAsync(operation, async (userId, db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException($"User {userId} not found");
            db.Users.Remove(user);
            await db.SaveChangesAsync(cancellationToken);
            return (object?)user.Id;
        });
    }

    private static void ValidateBatch<T>(BatchOperation<T> operation)
    {
        if (operation.Items.Count == 0)
        {
            throw new InvalidOperationException("Batch operation must contain at least one item");
        }

        var uniqueIds = new HashSet<string>(operation.Items.Select(item => item.Id));
        if (uniqueIds.Count != operation.Items.Count)
        {
            throw new InvalidOperationException("Batch operation contains duplicate IDs");
        }

        switch (operation.Type)
        {
            case BatchOperationType.Create:
                ValidateCreateOperation(operation);
                break;
            case BatchOperationType.Update:
                ValidateUpdateOperation(operation);
                break;
            case BatchOperationType.Delete:
                ValidateDeleteOperation(operation);
                break;
        }
    }

    private static void ValidateCreateOperation<T>(BatchOperation<T> operation)
    {
        foreach (var item in operation.Items)
        {
            if (item.Data is null)
            {
                throw new InvalidOperationException($"Item {item.Id} is missing required data");
            }
        }
    }

    private static void ValidateUpdateOperation<T>(BatchOperation<T> operation)
    {
        foreach (var item in operation.Items)
        {
            if (item.Data is null || string.IsNullOrEmpty(item.Id))
            {
                throw new InvalidOperationException($"Item {item.Id} is missing required data or ID");
            }
        }
    }

    private static void ValidateDeleteOperation<T>(BatchOperation<T> operation)
    {
        foreach (var item in operation.Items)
        {
            if (string.IsNullOrEmpty(item.Id))
            {
                throw new InvalidOperationException("Delete operation items must have valid IDs");
            }
        }
    }
}
