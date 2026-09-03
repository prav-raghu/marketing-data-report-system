using CustomerApi.Auth;
using CustomerApi.Dtos;
using CustomerApi.Services;
using CustomerApi.Validators;
using FluentValidation;

namespace CustomerApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/register", async (
            RegisterRequestDto body,
            IValidator<RegisterRequestDto> validator,
            AuthService authService,
            HttpContext context) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            body.Ip = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var result = await authService.RegisterAsync(body);
            return Results.Json(
                result,
                statusCode: result.IsSuccessful ? StatusCodes.Status201Created : StatusCodes.Status400BadRequest);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/login", async (LoginRequestDto body, IValidator<LoginRequestDto> validator, AuthService authService) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var result = await authService.LoginAsync(body);
            return Results.Json(
                result,
                statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
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
            return Results.Json(new { isSuccessful = true, data = tokens });
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/logout", async (HttpContext context, AuthService authService, LogoutRequestDto? body) =>
        {
            var currentUser = context.GetCurrentUser();
            var authHeader = context.Request.Headers.Authorization.ToString();
            var accessToken = authHeader.StartsWith("Bearer ", StringComparison.Ordinal) ? authHeader["Bearer ".Length..] : null;
            await authService.LogoutAsync(currentUser?.Id ?? string.Empty, accessToken, body?.RefreshToken);
            return Results.NoContent();
        });

        group.MapGet("/verify/{token}", async (string token, AuthService authService) =>
        {
            var result = await authService.VerifyEmailAsync(token);
            return Results.Json(
                result,
                statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).AllowAnonymous();

        group.MapPost("/resend-verification-email", async (ResendVerificationEmailDto body, AuthService authService) =>
        {
            var result = await authService.ResendVerificationEmailAsync(body.Email);
            return Results.Json(result);
        }).AllowAnonymous().RequireRateLimiting("sensitive");
    }
}
