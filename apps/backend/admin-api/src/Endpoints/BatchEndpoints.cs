using AdminApi.Auth;
using AdminApi.Dtos;
using AdminApi.Services;
using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Endpoints;

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/batch").WithMetadata(new RequirePermissionsAttribute(PermissionName.BatchWrite));

        group.MapPost("/users/create", async (BulkCreateUsersDto body, BatchOperationService batchService) =>
        {
            if (body.Users.Count == 0)
            {
                return Results.Json(new { isSuccessful = false, message = "No users provided" }, statusCode: StatusCodes.Status400BadRequest);
            }
            var result = await batchService.BulkCreateUsersAsync(body.Users);
            return Results.Json(
                new { isSuccessful = result.Failed == 0, data = result },
                statusCode: result.Failed > 0 ? StatusCodes.Status207MultiStatus : StatusCodes.Status200OK);
        });

        group.MapPost("/users/update-status", async (BulkUpdateStatusDto body, BatchOperationService batchService) =>
        {
            if (body.Updates.Count == 0)
            {
                return Results.Json(new { isSuccessful = false, message = "No updates provided" }, statusCode: StatusCodes.Status400BadRequest);
            }
            var result = await batchService.BulkUpdateUserStatusAsync(body.Updates);
            return Results.Json(
                new { isSuccessful = result.Failed == 0, data = result },
                statusCode: result.Failed > 0 ? StatusCodes.Status207MultiStatus : StatusCodes.Status200OK);
        });

        group.MapPost("/users/delete", async (BulkDeleteUsersDto body, BatchOperationService batchService) =>
        {
            if (body.UserIds.Count == 0)
            {
                return Results.Json(new { isSuccessful = false, message = "No user IDs provided" }, statusCode: StatusCodes.Status400BadRequest);
            }
            var result = await batchService.BulkDeleteUsersAsync(body.UserIds);
            return Results.Json(
                new { isSuccessful = result.Failed == 0, data = result },
                statusCode: result.Failed > 0 ? StatusCodes.Status207MultiStatus : StatusCodes.Status200OK);
        });

        group.MapPost("/custom", async (CustomBatchDto body, BatchOperationService batchService) =>
        {
            if (body.Items.Count == 0)
            {
                return Results.Json(new { isSuccessful = false, message = "No items provided" }, statusCode: StatusCodes.Status400BadRequest);
            }
            var operation = new BatchOperation<IReadOnlyDictionary<string, object?>>
            {
                Type = body.Operation,
                Items = body.Items
                    .Select(item => new BatchOperationItem<IReadOnlyDictionary<string, object?>>(item.Id, item.Data))
                    .ToList(),
            };
            var result = await batchService.ExecuteBatchAsync(operation, item => Task.FromResult<object?>(item));
            return Results.Json(
                new { isSuccessful = result.Failed == 0, data = result },
                statusCode: result.Failed > 0 ? StatusCodes.Status207MultiStatus : StatusCodes.Status200OK);
        });
    }
}
