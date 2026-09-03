using AdminApi.Auth;
using AdminApi.Dtos;
using AdminApi.Services;
using AdminApi.Validators;
using FluentValidation;

namespace AdminApi.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users");

        group.MapGet("/roles", async (UserService userService) => Results.Ok(await userService.GetUserRolesAsync()));

        group.MapGet("/statuses", async (UserService userService) => Results.Ok(await userService.GetUserStatusesAsync()));

        group.MapPost("/onboarding", async (
            OnboardingRequestDto body,
            IValidator<OnboardingRequestDto> validator,
            UserService userService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await userService.OnboardUserAsync(body);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        });

        group.MapPost("/resend-verification", async (
            OnboardingRequestDto body,
            IValidator<OnboardingRequestDto> validator,
            UserService userService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await userService.ResendVerificationEmailAsync(body);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        });

        group.MapGet("/check-email/{email}", async (string email, UserService userService) =>
        {
            var available = await userService.IsEmailAvailableAsync(email);
            return Results.Ok(new { email, available });
        }).RequireRateLimiting("sensitive");

        group.MapGet("/check-username/{username}", async (string username, UserService userService) =>
        {
            var available = await userService.IsUsernameAvailableAsync(username);
            return Results.Ok(new { username, available });
        }).RequireRateLimiting("sensitive");

        group.MapPut("/profile", async (
            UpdateProfileRequestDto body,
            IValidator<UpdateProfileRequestDto> validator,
            UserService userService,
            HttpContext context) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await userService.UpdateProfileAsync(currentUser.Id, body);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        });

        group.MapPost("/change-password", async (
            ChangePasswordRequestDto body,
            IValidator<ChangePasswordRequestDto> validator,
            UserService userService,
            HttpContext context) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            if (body.NewPassword != body.ConfirmPassword)
            {
                return Results.Json(new { isSuccessful = false, message = "Passwords do not match" }, statusCode: StatusCodes.Status400BadRequest);
            }
            var result = await userService.ChangePasswordAsync(currentUser.Id, body.CurrentPassword, body.NewPassword);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).RequireRateLimiting("sensitive");

        group.MapPost("/2fa/setup", async (UserService userService, HttpContext context) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var result = await userService.Setup2FAAsync(currentUser.Id);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).RequireRateLimiting("sensitive");

        group.MapPost("/2fa/verify", async (
            Verify2FARequestDto body,
            IValidator<Verify2FARequestDto> validator,
            UserService userService,
            HttpContext context) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await userService.Verify2FAAsync(currentUser.Id, body.Token);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).RequireRateLimiting("sensitive");

        group.MapPost("/2fa/disable", async (
            Disable2FARequestDto body,
            IValidator<Disable2FARequestDto> validator,
            UserService userService,
            HttpContext context) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await userService.Disable2FAAsync(currentUser.Id, body.Token);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).RequireRateLimiting("sensitive");

        group.MapGet("/{userId}/details", async (string userId, UserService userService) =>
        {
            var result = await userService.GetUserDetailsAsync(userId);
            if (result is null)
            {
                return Results.Json(new { isSuccessful = false, message = "User not found" }, statusCode: StatusCodes.Status404NotFound);
            }
            return Results.Ok(result);
        }).RequireRateLimiting("adminOperations");
    }
}
