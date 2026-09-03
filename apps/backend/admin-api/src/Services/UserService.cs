using System.Security.Cryptography;
using System.Text;
using AdminApi.Configuration;
using AdminApi.Dtos;
using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Database.Entities;
using DotNetMonoRepoTemplate.Email;
using DotNetMonoRepoTemplate.Types;
using OtpNet;
using QRCoder;

namespace AdminApi.Services;

public sealed record UserProfileDto
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Avatar { get; init; }
    public string? GenderId { get; init; }
    public int? Age { get; init; }
    public required bool AllowEmailCommunications { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required DateTime LastSeen { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public RoleSummaryDto? Roles { get; init; }
    public StatusSummaryDto? Status { get; init; }
}

public sealed record AuthorizedAdminUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
}

public sealed class UserService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly AdminApiOptions _options;

    public UserService(AppDbContext db, IEmailService emailService, AdminApiOptions options)
    {
        _db = db;
        _emailService = emailService;
        _options = options;
    }

    private string EncryptTotpSecret(string plaintext)
    {
        var key = Convert.FromHexString(_options.TwoFactorEncryptionKey);
        var iv = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(iv, plaintextBytes, ciphertext, tag);
        return $"{Convert.ToHexStringLower(iv)}:{Convert.ToHexStringLower(tag)}:{Convert.ToHexStringLower(ciphertext)}";
    }

    private string DecryptTotpSecret(string encrypted)
    {
        var parts = encrypted.Split(':');
        var iv = Convert.FromHexString(parts[0]);
        var tag = Convert.FromHexString(parts[1]);
        var ciphertext = Convert.FromHexString(parts[2]);
        var key = Convert.FromHexString(_options.TwoFactorEncryptionKey);
        var plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(iv, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public async Task<GetUsersPagedResponseDto> GetOnlineUsersAsync(GetUsersPagedRequestDto model, CancellationToken cancellationToken = default)
    {
        var query = _db.Users.Include(u => u.Status).Include(u => u.Roles).AsQueryable();

        if (model.RolesToFilterBy is { Count: > 0 })
        {
            query = query.Where(u => u.Roles != null && model.RolesToFilterBy.Contains(u.Roles.Name));
        }
        if (!string.IsNullOrWhiteSpace(model.SearchQuery))
        {
            var search = $"%{model.SearchQuery}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, search) || EF.Functions.ILike(u.Username, search));
        }

        var total = await query.CountAsync(cancellationToken);

        var ordered = (model.SortBy, model.SortOrder) switch
        {
            ("username", "asc") => query.OrderBy(u => u.Username),
            ("username", _) => query.OrderByDescending(u => u.Username),
            ("email", "asc") => query.OrderBy(u => u.Email),
            ("email", _) => query.OrderByDescending(u => u.Email),
            ("createdAt", "asc") => query.OrderBy(u => u.CreatedAt),
            ("createdAt", _) => query.OrderByDescending(u => u.CreatedAt),
            (_, "asc") => query.OrderBy(u => u.Id),
            _ => query.OrderByDescending(u => u.Id),
        };

        var users = await ordered
            .Skip((model.Page - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Avatar = u.Avatar,
                IpAddress = u.IpAddress,
                CreatedAt = u.CreatedAt.ToString("o"),
                LastSeen = u.LastSeen.ToString("o"),
                StatusName = u.Status != null ? u.Status.Name : null,
                RoleName = u.Roles != null ? u.Roles.Name : null,
            })
            .ToListAsync(cancellationToken);

        return new GetUsersPagedResponseDto
        {
            IsSuccessful = true,
            Message = "Users retrieved successfully",
            Users = users,
            Total = total,
            Page = model.Page,
            PageSize = model.PageSize,
            TotalPages = (int)Math.Ceiling(total / (double)model.PageSize),
        };
    }

    public Task<int> GetOnlineUsersCountAsync(CancellationToken cancellationToken = default) =>
        _db.Users.CountAsync(u => u.Status != null && u.Status.Name == "Online", cancellationToken);

    public Task<UserProfileDto?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u => u.Id == userId && u.Roles != null && RoleName.AdminTierRoles.Contains(u.Roles.Name))
            .Select(u => new UserProfileDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Avatar = u.Avatar,
                GenderId = u.GenderId,
                Age = u.Age,
                AllowEmailCommunications = u.AllowEmailCommunications,
                TwoFactorEnabled = u.TwoFactorEnabled,
                LastSeen = u.LastSeen,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Roles = u.Roles != null ? new RoleSummaryDto { Id = u.Roles.Id, Name = u.Roles.Name } : null,
                Status = u.Status != null ? new StatusSummaryDto { Id = u.Status.Id, Name = u.Status.Name } : null,
            })
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<AuthorizedAdminUser?> GetAuthorizedUserByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u =>
                u.Id == userId &&
                u.Status != null && u.Status.Name == "Online" &&
                u.Roles != null && RoleName.AdminTierRoles.Contains(u.Roles.Name))
            .Select(u => new AuthorizedAdminUser { Id = u.Id, Username = u.Username, Email = u.Email })
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GetUserRolesResponseDto> GetUserRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _db.Roles.OrderBy(r => r.Name)
            .Select(r => new RoleSummaryDto { Id = r.Id, Name = r.Name })
            .ToListAsync(cancellationToken);
        return new GetUserRolesResponseDto { IsSuccessful = true, Message = "Roles retrieved successfully", Data = roles };
    }

    public async Task<GetUserStatusesResponseDto> GetUserStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _db.UserStatuses.OrderBy(s => s.Name)
            .Select(s => new StatusSummaryDto { Id = s.Id, Name = s.Name })
            .ToListAsync(cancellationToken);
        return new GetUserStatusesResponseDto { IsSuccessful = true, Message = "Statuses retrieved successfully", Data = statuses };
    }

    public async Task<UserStatsResponseDto> GetUserStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var oneMonthAgo = now.AddMonths(-1);
        var twoMonthsAgo = now.AddMonths(-2);

        var thisMonth = await _db.Users.CountAsync(u => u.CreatedAt >= oneMonthAgo, cancellationToken);
        var prevMonth = await _db.Users.CountAsync(u => u.CreatedAt >= twoMonthsAgo && u.CreatedAt < oneMonthAgo, cancellationToken);
        var growth = (thisMonth - prevMonth) / (double)(prevMonth == 0 ? 1 : prevMonth) * 100;

        return new UserStatsResponseDto { Total = thisMonth, Growth = (int)Math.Round(growth) };
    }

    public async Task<OnboardingResponseDto> OnboardUserAsync(OnboardingRequestDto model, CancellationToken cancellationToken = default)
    {
        var existingUser = await _db.Users.FirstOrDefaultAsync(
            u => u.Username == model.Username || u.Email == model.Email, cancellationToken);
        if (existingUser is not null)
        {
            var message = existingUser.Username == model.Username ? "Username already exists" : "Email already exists";
            return new OnboardingResponseDto { IsSuccessful = false, Message = message };
        }

        var authHash = Guid.NewGuid().ToString();
        var user = new User
        {
            Username = model.Username,
            Email = model.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 10),
            RoleId = model.RoleId,
            GenderId = model.Gender,
            Age = model.Age,
            AcceptTermsAndConditions = model.AcceptTermsAndConditions ?? false,
            AllowEmailCommunications = model.AllowEmailCommunications,
            IpAddress = string.Empty,
            UserStatusId = model.UserStatusId,
            AuthHash = authHash,
            AuthHashExpiration = DateTime.UtcNow.AddHours(24),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId, cancellationToken);
        if (role is null)
        {
            return new OnboardingResponseDto { IsSuccessful = false, Message = "User role not found" };
        }

        await _emailService.SendMailAsync(
            user.Email,
            "Admin Onboarding",
            "admin-onboarding-notification",
            new Dictionary<string, object?>
            {
                ["username"] = user.Username,
                ["dateLoggedIn"] = DateTime.UtcNow.ToString("G"),
                ["verificationLink"] = $"{_options.AdminWebUrl}/verify-email?auth_hash={authHash}",
            },
            cancellationToken);

        return new OnboardingResponseDto { IsSuccessful = true, Message = "User onboarded successfully" };
    }

    public async Task<OnboardingResponseDto> ResendVerificationEmailAsync(OnboardingRequestDto model, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.Status).FirstOrDefaultAsync(u => u.Email == model.Email, cancellationToken);
        if (user is null)
        {
            return new OnboardingResponseDto { IsSuccessful = false, Message = "User not found" };
        }
        if (user.Status?.Name != "Pending Verification")
        {
            return new OnboardingResponseDto { IsSuccessful = false, Message = "You cannot resend verification email for this user" };
        }

        var authHash = Guid.NewGuid().ToString();
        user.AuthHash = authHash;
        user.AuthHashExpiration = DateTime.UtcNow.AddHours(2);
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendMailAsync(
            user.Email,
            "Admin Onboarding",
            "admin-onboarding-notification",
            new Dictionary<string, object?>
            {
                ["username"] = user.Username,
                ["dateLoggedIn"] = DateTime.UtcNow.ToString("G"),
                ["verificationLink"] = $"{_options.AdminWebUrl}/verify-email?auth_hash={authHash}",
            },
            cancellationToken);

        return new OnboardingResponseDto { IsSuccessful = true, Message = "Verification email resent successfully" };
    }

    public async Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancellationToken = default) =>
        !await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<bool> IsUsernameAvailableAsync(string username, CancellationToken cancellationToken = default) =>
        !await _db.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<UpdateProfileResponseDto> UpdateProfileAsync(string userId, UpdateProfileRequestDto data, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new UpdateProfileResponseDto { IsSuccessful = false, Message = "User not found" };
        }

        if (data.Username != user.Username && await _db.Users.AnyAsync(u => u.Username == data.Username, cancellationToken))
        {
            return new UpdateProfileResponseDto { IsSuccessful = false, Message = "Username already taken" };
        }
        if (data.Email != user.Email && await _db.Users.AnyAsync(u => u.Email == data.Email, cancellationToken))
        {
            return new UpdateProfileResponseDto { IsSuccessful = false, Message = "Email already taken" };
        }

        user.Username = data.Username;
        user.Email = data.Email;
        user.Avatar = data.Avatar;
        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateProfileResponseDto
        {
            IsSuccessful = true,
            Message = "Profile updated successfully",
            Data = new UpdateProfileData { Username = user.Username, Email = user.Email, Avatar = user.Avatar },
        };
    }

    public async Task<ChangePasswordResponseDto> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new ChangePasswordResponseDto { IsSuccessful = false, Message = "User not found" };
        }
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.Password))
        {
            return new ChangePasswordResponseDto { IsSuccessful = false, Message = "Current password is incorrect" };
        }

        user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 10);
        await _db.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResponseDto { IsSuccessful = true, Message = "Password changed successfully" };
    }

    public async Task<Setup2FAResponseDto> Setup2FAAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new Setup2FAResponseDto { IsSuccessful = false, Message = "User not found" };
        }

        var secretKey = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretKey);
        var otpauthUrl = $"otpauth://totp/Admin:{Uri.EscapeDataString(user.Email)}?secret={base32Secret}&issuer=Admin";

        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpauthUrl, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = pngQrCode.GetGraphic(20);
        var qrCodeDataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";

        user.TwoFactorSecret = EncryptTotpSecret(base32Secret);
        await _db.SaveChangesAsync(cancellationToken);

        return new Setup2FAResponseDto
        {
            IsSuccessful = true,
            Message = "2FA setup initiated",
            Data = new Setup2FAData { Secret = base32Secret, QrCode = qrCodeDataUrl },
        };
    }

    public async Task<bool> VerifyTotpCodeAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is not null && VerifyTotpCode(user, code);
    }

    private bool VerifyTotpCode(User user, string code)
    {
        if (user.TwoFactorSecret is null)
        {
            return false;
        }
        var secret = DecryptTotpSecret(user.TwoFactorSecret);
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    public async Task<Verify2FAResponseDto> Verify2FAAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.TwoFactorSecret is null)
        {
            return new Verify2FAResponseDto { IsSuccessful = false, Message = "User not found or 2FA not set up" };
        }
        if (!VerifyTotpCode(user, token))
        {
            return new Verify2FAResponseDto { IsSuccessful = false, Message = "Invalid verification code" };
        }

        user.TwoFactorEnabled = true;
        await _db.SaveChangesAsync(cancellationToken);

        return new Verify2FAResponseDto { IsSuccessful = true, Message = "2FA enabled successfully" };
    }

    public async Task<Disable2FAResponseDto> Disable2FAAsync(string userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.TwoFactorEnabled || user.TwoFactorSecret is null)
        {
            return new Disable2FAResponseDto { IsSuccessful = false, Message = "User not found or 2FA not enabled" };
        }
        if (!VerifyTotpCode(user, token))
        {
            return new Disable2FAResponseDto { IsSuccessful = false, Message = "Invalid verification code" };
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await _db.SaveChangesAsync(cancellationToken);

        return new Disable2FAResponseDto { IsSuccessful = true, Message = "2FA disabled successfully" };
    }

    public async Task<UserDetailsResponseDto?> GetUserDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.Status).Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new UserDetailsResponseDto
        {
            IsSuccessful = true,
            Message = "User details retrieved successfully",
            Data = new UserDetailsData
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Avatar = user.Avatar,
                GenderId = user.GenderId,
                Age = user.Age,
                AcceptTermsAndConditions = user.AcceptTermsAndConditions,
                AllowEmailCommunications = user.AllowEmailCommunications,
                IpAddress = user.IpAddress,
                LastSeen = user.LastSeen,
                IsActive = user.IsActive,
                TwoFactorEnabled = user.TwoFactorEnabled,
                UserStatusId = user.UserStatusId,
                RoleId = user.RoleId,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                CreatedBy = user.CreatedBy,
                ModifiedBy = user.ModifiedBy,
                Status = user.Status is not null ? new StatusSummaryDto { Id = user.Status.Id, Name = user.Status.Name } : null,
                Roles = user.Roles is not null ? new RoleSummaryDto { Id = user.Roles.Id, Name = user.Roles.Name } : null,
                IpAddresses = new[] { user.IpAddress },
            },
        };
    }
}
