using CustomerApi.Auth;
using CustomerApi.Services;

namespace CustomerApi.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users");

        group.MapGet("/", async (
            HttpContext context,
            UserService userService,
            string? gender,
            string? minAge,
            string? maxAge,
            string? limit,
            string? offset) =>
        {
            var currentUser = context.GetCurrentUser();
            var filters = new UserFilters
            {
                GenderId = gender,
                MinAge = int.TryParse(minAge, out var min) ? min : null,
                MaxAge = int.TryParse(maxAge, out var max) ? max : null,
                Limit = int.TryParse(limit, out var take) ? take : 20,
                Offset = int.TryParse(offset, out var skip) ? skip : 0,
            };
            var users = await userService.GetUsersAsync(filters, currentUser?.Id);
            return Results.Ok(new { isSuccessful = true, data = users });
        });

        group.MapGet("/{userId}", async (string userId, UserService userService) =>
        {
            var user = await userService.GetUserByIdAsync(userId);
            if (user is null)
            {
                return Results.Json(
                    new { isSuccessful = false, message = "User not found" },
                    statusCode: StatusCodes.Status404NotFound);
            }
            return Results.Ok(new { isSuccessful = true, data = user });
        });
    }
}
