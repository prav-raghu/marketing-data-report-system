using AdminApi.Configuration;
using AdminApi.Dtos;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using StackExchange.Redis;

namespace AdminApi.Services;

public sealed class AuthService
{
    private const string DummyHash = "$2b$10$abcdefghijklmnopqrstuvuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu";
    private const int LoginMaxAttempts = 5;
    private const int LoginLockoutTtlSeconds = 900;
    private const int MfaMaxAttempts = 5;
    private const int MfaLockoutTtlSeconds = 300;

    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly UserService _userService;
    private readonly IConnectionMultiplexer _redis;
    private readonly AdminApiOptions _options;

    public AuthService(
        AppDbContext db,
        TokenService tokenService,
        IEmailService emailService,
        UserService userService,
        IConnectionMultiplexer redis,
        AdminApiOptions options)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
        _userService = userService;
        _redis = redis;
        _options = options;
    }

    private IDatabase? Database => _redis.IsConnected ? _redis.GetDatabase() : null;

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto model, CancellationToken cancellationToken = default)
    {
        var lockKey = $"login:fail:{model.Email}";
        var db = Database;
        if (db is not null)
        {
            var lockCount = await db.StringGetAsync(lockKey);
            if (lockCount.HasValue && int.TryParse(lockCount, out var attempts) && attempts >= LoginMaxAttempts)
            {
                return new LoginResponseDto
                {
                    IsSuccessful = false,
                    Message = "Account temporarily locked due to too many failed attempts. Try again later.",
                };
            }
        }

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == model.Email, cancellationToken);
        var passwordToCompare = user?.Password ?? DummyHash;
        var passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, passwordToCompare);
        if (user is null || !passwordValid)
        {
            if (db is not null)
            {
                var count = await db.StringIncrementAsync(lockKey);
                if (count == 1)
                {
                    await db.KeyExpireAsync(lockKey, TimeSpan.FromSeconds(LoginLockoutTtlSeconds));
                }
            }
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid username or password" };
        }

        var userRole = user.Roles;
        if (userRole is null || !RoleName.AdminTierRoles.Contains(userRole.Name))
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid username or password" };
        }

        if (db is not null)
        {
            await db.KeyDeleteAsync(lockKey);
        }

        if (user.TwoFactorEnabled)
        {
            var mfaToken = _tokenService.GenerateMfaChallengeToken(user.Id);
            return new LoginResponseDto
            {
                IsSuccessful = true,
                Message = "Verification code required",
                Data = new LoginResponseData { MfaRequired = true, MfaToken = mfaToken },
            };
        }

        var tokens = _tokenService.GenerateToken(user, model.RememberMe, userRole.Name);
        await UpdateUserStatusAsync(user, "Online", cancellationToken);
        await SendLoginNotificationAsync(user, model.Ip, cancellationToken);

        return new LoginResponseDto
        {
            IsSuccessful = true,
            Message = "Login successful",
            Data = new LoginResponseData { AuthToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken, Username = user.Username },
        };
    }

    public async Task<LoginResponseDto> VerifyLoginMfaAsync(VerifyLoginMfaRequestDto model, CancellationToken cancellationToken = default)
    {
        var challenge = _tokenService.VerifyMfaChallengeToken(model.MfaToken);
        if (challenge is null)
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Verification session expired — please log in again" };
        }

        var lockKey = $"mfa:fail:{challenge.Id}";
        var db = Database;
        if (db is not null)
        {
            var lockCount = await db.StringGetAsync(lockKey);
            if (lockCount.HasValue && int.TryParse(lockCount, out var attempts) && attempts >= MfaMaxAttempts)
            {
                return new LoginResponseDto { IsSuccessful = false, Message = "Too many failed verification attempts. Please log in again." };
            }
        }

        var isValidCode = await _userService.VerifyTotpCodeAsync(challenge.Id, model.Code, cancellationToken);
        if (!isValidCode)
        {
            if (db is not null)
            {
                var count = await db.StringIncrementAsync(lockKey);
                if (count == 1)
                {
                    await db.KeyExpireAsync(lockKey, TimeSpan.FromSeconds(MfaLockoutTtlSeconds));
                }
            }
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid verification code" };
        }
        if (db is not null)
        {
            await db.KeyDeleteAsync(lockKey);
        }

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == challenge.Id, cancellationToken);
        if (user is null)
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid verification session" };
        }
        var userRole = user.Roles;
        if (userRole is null || !RoleName.AdminTierRoles.Contains(userRole.Name))
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid verification session" };
        }

        var tokens = _tokenService.GenerateToken(user, model.RememberMe, userRole.Name);
        await UpdateUserStatusAsync(user, "Online", cancellationToken);
        await SendLoginNotificationAsync(user, model.Ip, cancellationToken);

        return new LoginResponseDto
        {
            IsSuccessful = true,
            Message = "Login successful",
            Data = new LoginResponseData { AuthToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken, Username = user.Username },
        };
    }

    private Task SendLoginNotificationAsync(User user, string? ip, CancellationToken cancellationToken)
    {
        if (!user.AllowEmailCommunications)
        {
            return Task.CompletedTask;
        }
        return _emailService.SendMailAsync(
            user.Email,
            "Admin Login Notification",
            "admin-login-notification",
            new Dictionary<string, object?>
            {
                ["username"] = user.Username,
                ["dateLoggedIn"] = DateTime.UtcNow.ToString("G"),
                ["ipAddress"] = ip ?? "unknown",
            },
            cancellationToken);
    }

    public Task<TokenPair?> RefreshTokenAsync(string token, bool rememberMe) => _tokenService.RefreshTokenAsync(token, rememberMe);

    public async Task<LogOutResponseDto> LogoutAsync(string userId, string? accessToken, string? refreshToken, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new LogOutResponseDto { IsSuccessful = false, Message = "Error occurred" };
        }

        var offlineStatus = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == "Offline", cancellationToken);
        if (offlineStatus is not null)
        {
            user.UserStatusId = offlineStatus.Id;
        }
        user.LastSeen = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _tokenService.LogoutAsync(userId, accessToken, refreshToken);

        return new LogOutResponseDto { IsSuccessful = true, Message = "Successfully Logged Out" };
    }

    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordRequestDto model, CancellationToken cancellationToken = default)
    {
        if (model.ConfirmPassword is not null && model.Password != model.ConfirmPassword)
        {
            return new ResetPasswordResponseDto { IsSuccessful = false, Message = "New password and confirm password do not match" };
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.AuthHash == model.Token, cancellationToken);
        if (user is null)
        {
            return new ResetPasswordResponseDto { IsSuccessful = false, Message = "Invalid or expired token" };
        }
        if (user.AuthHashExpiration is not null && user.AuthHashExpiration < DateTime.UtcNow)
        {
            return new ResetPasswordResponseDto { IsSuccessful = false, Message = "Token has expired" };
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 10);
        user.AuthHash = null;
        user.AuthHashExpiration = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _tokenService.LogoutAsync(user.Id, null, null);
        await _emailService.SendMailAsync(
            user.Email,
            "Admin Password Reset",
            "admin-password-reset",
            new Dictionary<string, object?> { ["username"] = user.Username },
            cancellationToken);

        return new ResetPasswordResponseDto { IsSuccessful = true, Message = "Password reset successful" };
    }

    public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        const string neutralMessage = "If that email exists, a reset link has been sent";

        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return new ForgotPasswordResponseDto { IsSuccessful = true, Message = neutralMessage };
        }
        var userRole = user.Roles;
        if (userRole is null || !RoleName.AdminTierRoles.Contains(userRole.Name))
        {
            return new ForgotPasswordResponseDto { IsSuccessful = true, Message = neutralMessage };
        }

        var resetHash = Guid.NewGuid().ToString();
        user.AuthHash = resetHash;
        user.AuthHashExpiration = DateTime.UtcNow.AddMinutes(_options.PasswordResetExpirationMinutes);
        await _db.SaveChangesAsync(cancellationToken);

        var sent = await _emailService.SendMailAsync(
            email,
            "Admin Password Reset",
            "admin-forgot-password",
            new Dictionary<string, object?>
            {
                ["username"] = user.Username,
                ["resetLink"] = $"{_options.AdminWebUrl}/reset-password?token={resetHash}",
            },
            cancellationToken);

        return sent
            ? new ForgotPasswordResponseDto { IsSuccessful = true, Message = "Password reset email sent successfully" }
            : new ForgotPasswordResponseDto { IsSuccessful = false, Message = "Error occurred" };
    }

    private async Task UpdateUserStatusAsync(User user, string statusName, CancellationToken cancellationToken)
    {
        var status = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == statusName, cancellationToken);
        if (status is not null)
        {
            user.UserStatusId = status.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<UserProfileDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default) =>
        _userService.GetUserProfileAsync(userId, cancellationToken);
}
