using CustomerApi.Services;
using Microsoft.AspNetCore.Authorization;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Auth;

public sealed class AuthGuardMiddleware
{
    private readonly RequestDelegate _next;

    public AuthGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        TokenService tokenService,
        UserService userService,
        IHostEnvironment environment)
    {
        if (IsPublic(context) || IsDocsInDevelopment(context, environment))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        var parts = header.Split(' ', 2);
        var token = parts.Length == 2 ? parts[1] : null;
        if (string.IsNullOrEmpty(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var payload = tokenService.VerifyAccessToken(token);
        if (payload is null || payload.Scope != TokenScope.Customer)
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        if (payload.Jti is not null && await tokenService.IsTokenBlacklistedAsync(payload.Jti))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        if (await tokenService.IsSessionInvalidatedAsync(payload.Id, payload.Iat))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var user = await userService.GetAuthorizedUserByIdAsync(payload.Id);
        if (user is null)
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var currentUser = new CurrentUser
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = payload.Role,
            Permissions = payload.Permissions,
            Scope = payload.Scope,
        };
        context.Items["CurrentUser"] = currentUser;

        var requiredPermissions = context.GetEndpoint()?.Metadata.GetMetadata<RequirePermissionsAttribute>()?.Permissions;
        if (requiredPermissions is { Count: > 0 })
        {
            var userPermissions = new HashSet<string>(currentUser.Permissions);
            if (!requiredPermissions.All(userPermissions.Contains))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { isSuccessful = false, message = "Forbidden: insufficient permissions" });
                return;
            }
        }

        await _next(context);
    }

    private static bool IsPublic(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null;

    private static bool IsDocsInDevelopment(HttpContext context, IHostEnvironment environment) =>
        !environment.IsProduction() && context.Request.Path.StartsWithSegments("/docs");

    private static Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
    }
}

public static class CurrentUserHttpContextExtensions
{
    public static CurrentUser? GetCurrentUser(this HttpContext context) =>
        context.Items.TryGetValue("CurrentUser", out var value) ? value as CurrentUser : null;
}
