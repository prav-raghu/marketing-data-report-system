using AdminApi.Auth;
using AdminApi.Dtos;
using AdminApi.Services;
using AdminApi.Validators;
using FluentValidation;

namespace AdminApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (
            LoginRequestDto body,
            IValidator<LoginRequestDto> validator,
            AuthService authService,
            HttpContext context) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var model = body with { Ip = context.Connection.RemoteIpAddress?.ToString() };
            var result = await authService.LoginAsync(model);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/verify-login-mfa", async (
            VerifyLoginMfaRequestDto body,
            IValidator<VerifyLoginMfaRequestDto> validator,
            AuthService authService,
            HttpContext context) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var model = body with { Ip = context.Connection.RemoteIpAddress?.ToString() };
            var result = await authService.VerifyLoginMfaAsync(model);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
            RefreshTokenRequestDto body,
            IValidator<RefreshTokenRequestDto> validator,
            AuthService authService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var tokens = await authService.RefreshTokenAsync(body.RefreshToken, body.RememberMe);
            if (tokens is null)
            {
                return Results.Json(
                    new { isSuccessful = false, message = "Invalid or expired refresh token" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            return Results.Json(new { isSuccessful = true, data = new { accessToken = tokens.AccessToken, refreshToken = tokens.RefreshToken } });
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequestDto body,
            IValidator<ForgotPasswordRequestDto> validator,
            AuthService authService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await authService.ForgotPasswordAsync(body.Email);
            return Results.Json(result);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/reset-password", async (
            ResetPasswordRequestDto body,
            IValidator<ResetPasswordRequestDto> validator,
            AuthService authService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await authService.ResetPasswordAsync(body);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapGet("/logout", async (HttpContext context, AuthService authService) =>
        {
            var currentUser = context.GetCurrentUser();
            var authHeader = context.Request.Headers.Authorization.ToString();
            var accessToken = authHeader.Replace("Bearer ", string.Empty).Trim();
            var result = await authService.LogoutAsync(currentUser?.Id ?? string.Empty, accessToken, null);
            return Results.Json(result);
        });

        group.MapGet("/me", async (HttpContext context, AuthService authService) =>
        {
            var currentUser = context.GetCurrentUser();
            if (currentUser is null)
            {
                return Results.Json(new { isSuccessful = false, message = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            var result = await authService.GetCurrentUserAsync(currentUser.Id);
            return Results.Ok(result);
        });
    }
}
