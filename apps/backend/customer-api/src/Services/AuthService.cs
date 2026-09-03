using System.Text.Json;
using System.Text.RegularExpressions;
using CustomerApi.Configuration;
using CustomerApi.Dtos;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using DotNetMonoRepoTemplate.Utilities;
using StackExchange.Redis;

namespace CustomerApi.Services;

public sealed partial class AuthService
{
    private const string DummyHash = "$2b$10$abcdefghijklmnopqrstuvuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu";
    private const int LoginMaxAttempts = 5;
    private const int LoginLockoutTtlSeconds = 900;
    private const int IpLoginMaxAttempts = 20;
    private const int IpLoginLockoutTtlSeconds = 900;

    private static readonly IReadOnlyList<string> ProhibitedEmailDomains = LoadProhibitedEmailDomains();

    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConnectionMultiplexer _redis;
    private readonly CustomerApiOptions _options;

    public AuthService(
        AppDbContext db,
        TokenService tokenService,
        IEmailService emailService,
        IConnectionMultiplexer redis,
        CustomerApiOptions options)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
        _redis = redis;
        _options = options;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto model, CancellationToken cancellationToken = default)
    {
        if (!model.AcceptTermsAndConditions)
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "You must accept the terms and conditions to register" };
        }
        if (!IsUsernameValidForCharacters(model.Username))
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "Username contains invalid characters" };
        }
        if (await IsUsernameTakenAsync(model.Username, cancellationToken))
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "Username is already taken" };
        }
        if (!IsEmailDomainAllowed(model.Email))
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "Email address is not allowed" };
        }
        if (await IsEmailRegisteredAsync(model.Email, cancellationToken))
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "Email is already registered" };
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == RoleName.ChatUser, cancellationToken);
        var status = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == "Pending Verification", cancellationToken);
        if (role is null || status is null)
        {
            return new RegisterResponseDto { IsSuccessful = false, Message = "User registration failed", DateTimeStamp = DateTime.UtcNow };
        }

        var authHash = Guid.NewGuid().ToString();
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = model.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: PasswordHashing.WorkFactor),
            Email = model.Email,
            Age = model.Age,
            GenderId = model.GenderId,
            AcceptTermsAndConditions = model.AcceptTermsAndConditions,
            AllowEmailCommunications = model.AllowEmailCommunications,
            IpAddress = model.Ip,
            RoleId = role.Id,
            UserStatusId = status.Id,
            AuthHash = authHash,
            AuthHashExpiration = DateTime.UtcNow.AddHours(2),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var verificationLink = $"{_options.CustomerWebUrl}/verify-email?email={Uri.EscapeDataString(user.Email)}&token={authHash}";
        var sent = await SendVerificationEmailAsync(user.Email, user.Username, verificationLink, cancellationToken);
        if (!sent)
        {
            return new RegisterResponseDto
            {
                IsSuccessful = false,
                Message = "Failed to send user verification email",
                DateTimeStamp = DateTime.UtcNow,
            };
        }

        return new RegisterResponseDto
        {
            IsSuccessful = true,
            Message = "Account registered successfully please check your email for verification instructions.",
            DateTimeStamp = DateTime.UtcNow,
            Data = new RegisterResponseData { Email = user.Email },
        };
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto model, CancellationToken cancellationToken = default)
    {
        var lockKey = $"login:fail:{model.Username}";
        var ipLockKey = string.IsNullOrEmpty(model.Ip) ? null : $"login:fail:ip:{model.Ip}";
        var db = _redis.IsConnected ? _redis.GetDatabase() : null;
        if (db is null)
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Login temporarily unavailable. Please try again shortly." };
        }

        var lockCount = await db.StringGetAsync(lockKey);
        if (lockCount.HasValue && int.TryParse((string?)lockCount, out var attempts) && attempts >= LoginMaxAttempts)
        {
            return new LoginResponseDto
            {
                IsSuccessful = false,
                Message = "Account temporarily locked due to too many failed attempts. Try again later.",
            };
        }

        if (ipLockKey is not null)
        {
            var ipLockCount = await db.StringGetAsync(ipLockKey);
            if (ipLockCount.HasValue && int.TryParse((string?)ipLockCount, out var ipAttempts) && ipAttempts >= IpLoginMaxAttempts)
            {
                return new LoginResponseDto
                {
                    IsSuccessful = false,
                    Message = "Too many failed login attempts from this network. Try again later.",
                };
            }
        }

        var user = await _db.Users
            .Include(u => u.Status)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == model.Username, cancellationToken);

        var passwordToCompare = user?.Password ?? DummyHash;
        var passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, passwordToCompare);
        if (user is null || !passwordValid)
        {
            var count = await db.StringIncrementAsync(lockKey);
            if (count == 1)
            {
                await db.KeyExpireAsync(lockKey, TimeSpan.FromSeconds(LoginLockoutTtlSeconds));
            }
            if (ipLockKey is not null)
            {
                var ipCount = await db.StringIncrementAsync(ipLockKey);
                if (ipCount == 1)
                {
                    await db.KeyExpireAsync(ipLockKey, TimeSpan.FromSeconds(IpLoginLockoutTtlSeconds));
                }
            }
            return new LoginResponseDto { IsSuccessful = false, Message = "Invalid username or password" };
        }

        if (user.Status?.Name == "Pending Verification")
        {
            return new LoginResponseDto { IsSuccessful = false, Message = "Please verify your email before logging in" };
        }

        await db.KeyDeleteAsync(lockKey);
        if (ipLockKey is not null)
        {
            await db.KeyDeleteAsync(ipLockKey);
        }

        var onlineStatus = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == "Online", cancellationToken);
        if (onlineStatus is not null)
        {
            user.UserStatusId = onlineStatus.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var tokens = _tokenService.GenerateToken(user.Id, user.Username, user.Roles?.Name ?? string.Empty, model.RememberMe);

        return new LoginResponseDto
        {
            IsSuccessful = true,
            Message = "Login successful",
            Data = new LoginResponseData
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                UserName = user.Username,
                Email = user.Email,
            },
        };
    }

    public async Task<RefreshResponseData?> RefreshTokenAsync(string token, bool rememberMe)
    {
        var tokens = await _tokenService.RefreshTokenAsync(token, rememberMe);
        return tokens is null
            ? null
            : new RefreshResponseData { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken };
    }

    public async Task LogoutAsync(string userId, string? accessToken, string? refreshToken, CancellationToken cancellationToken = default)
    {
        await _tokenService.LogoutAsync(userId, accessToken, refreshToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }
        var offlineStatus = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == "Offline", cancellationToken);
        if (offlineStatus is not null)
        {
            user.LastSeen = DateTime.UtcNow;
            user.UserStatusId = offlineStatus.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<VerifyEmailResponseDto> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.Status).FirstOrDefaultAsync(u => u.AuthHash == token, cancellationToken);
        if (user is null)
        {
            return new VerifyEmailResponseDto { IsSuccessful = false, Message = "Invalid or expired token" };
        }
        if (user.Status?.Name == "Verified")
        {
            return new VerifyEmailResponseDto { IsSuccessful = false, Message = "Email is already verified" };
        }
        if (user.AuthHashExpiration is null || user.AuthHashExpiration <= DateTime.UtcNow)
        {
            return new VerifyEmailResponseDto { IsSuccessful = false, Message = "Invalid or expired token" };
        }

        var verifiedStatus = await _db.UserStatuses.FirstOrDefaultAsync(s => s.Name == "Verified", cancellationToken);
        if (verifiedStatus is not null)
        {
            user.UserStatusId = verifiedStatus.Id;
        }
        user.AuthHash = null;
        user.AuthHashExpiration = null;
        await _db.SaveChangesAsync(cancellationToken);

        return new VerifyEmailResponseDto { IsSuccessful = true, Message = "Email verified successfully" };
    }

    public async Task<VerifyEmailResponseDto> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string neutralMessage = "If that email is registered and unverified, a new link has been sent";
        var user = await _db.Users.Include(u => u.Status).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null || user.Status?.Name == "Verified")
        {
            return new VerifyEmailResponseDto { IsSuccessful = true, Message = neutralMessage };
        }

        var authHash = Guid.NewGuid().ToString();
        user.AuthHash = authHash;
        user.AuthHashExpiration = DateTime.UtcNow.AddHours(2);
        await _db.SaveChangesAsync(cancellationToken);

        var verificationLink = $"{_options.CustomerWebUrl}/verify-email?email={Uri.EscapeDataString(user.Email)}&token={authHash}";
        await SendVerificationEmailAsync(user.Email, user.Username, verificationLink, cancellationToken);

        return new VerifyEmailResponseDto { IsSuccessful = true, Message = neutralMessage };
    }

    private Task<bool> SendVerificationEmailAsync(
        string email,
        string username,
        string verificationLink,
        CancellationToken cancellationToken) =>
        _emailService.SendMailAsync(
            email,
            "Verify your email",
            "verify-email",
            new Dictionary<string, object?> { ["email"] = email, ["username"] = username, ["verificationLink"] = verificationLink },
            cancellationToken);

    private Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken) =>
        _db.Users.AnyAsync(u => u.Username == username, cancellationToken);

    private Task<bool> IsEmailRegisteredAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    private static bool IsUsernameValidForCharacters(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }
        if (username.Contains('%') || username.Contains('\\'))
        {
            return false;
        }
        return UsernameCharacterRegex().IsMatch(username);
    }

    private static bool IsEmailDomainAllowed(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return false;
        }
        var domain = email[(atIndex + 1)..].ToLowerInvariant();
        return !ProhibitedEmailDomains.Contains(domain);
    }

    private static IReadOnlyList<string> LoadProhibitedEmailDomains()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "prohibited-email-domains.json");
        if (!File.Exists(filePath))
        {
            return Array.Empty<string>();
        }
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
        {
            return dataElement.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToList();
        }
        return Array.Empty<string>();
    }

    [GeneratedRegex("^[A-Za-z_ ]+$")]
    private static partial Regex UsernameCharacterRegex();
}
