using DotNetMonoRepoTemplate.Types;

namespace AdminApi.Dtos;

public sealed record GetUsersPagedRequestDto
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public string? SearchQuery { get; init; }
    public string SortBy { get; init; } = "id";
    public string SortOrder { get; init; } = "desc";
    public IReadOnlyList<string>? RolesToFilterBy { get; init; }
}

public sealed record UserListItemDto
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Avatar { get; init; }
    public string? IpAddress { get; init; }
    public required string CreatedAt { get; init; }
    public string? LastSeen { get; init; }
    public string? StatusName { get; init; }
    public string? RoleName { get; init; }
}

public sealed record GetUsersPagedResponseDto : ResponseDto
{
    public IReadOnlyList<UserListItemDto> Users { get; init; } = Array.Empty<UserListItemDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed record RoleSummaryDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record GetUserRolesResponseDto : ResponseDto
{
    public IReadOnlyList<RoleSummaryDto> Data { get; init; } = Array.Empty<RoleSummaryDto>();
}

public sealed record StatusSummaryDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public sealed record GetUserStatusesResponseDto : ResponseDto
{
    public IReadOnlyList<StatusSummaryDto> Data { get; init; } = Array.Empty<StatusSummaryDto>();
}

public sealed record UserStatsResponseDto
{
    public required int Total { get; init; }
    public required int Growth { get; init; }
}

public sealed record OnboardingRequestDto
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? Gender { get; init; }
    public int? Age { get; init; }
    public string? Country { get; init; }
    public string? Region { get; init; }
    public required bool AllowEmailCommunications { get; init; }
    public bool? AcceptTermsAndConditions { get; init; }
    public required string IpAddress { get; init; }
    public required string UserStatusId { get; init; }
    public required string RoleId { get; init; }
    public string? JoinDate { get; init; }
    public string? Avatar { get; init; }
}

public sealed record OnboardingResponseDto : ResponseDto;

public sealed record UpdateProfileRequestDto
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Avatar { get; init; }
}

public sealed record UpdateProfileData
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Avatar { get; init; }
}

public sealed record UpdateProfileResponseDto : ResponseDto
{
    public UpdateProfileData? Data { get; init; }
}

public sealed record ChangePasswordRequestDto
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
    public required string ConfirmPassword { get; init; }
}

public sealed record ChangePasswordResponseDto : ResponseDto;

public sealed record Setup2FAData
{
    public required string Secret { get; init; }
    public required string QrCode { get; init; }
}

public sealed record Setup2FAResponseDto : ResponseDto
{
    public Setup2FAData? Data { get; init; }
}

public sealed record Verify2FARequestDto
{
    public required string Token { get; init; }
}

public sealed record Verify2FAResponseDto : ResponseDto;

public sealed record Disable2FARequestDto
{
    public required string Token { get; init; }
}

public sealed record Disable2FAResponseDto : ResponseDto;

public sealed record UserDetailsData
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public string? Avatar { get; init; }
    public string? GenderId { get; init; }
    public int? Age { get; init; }
    public required bool AcceptTermsAndConditions { get; init; }
    public required bool AllowEmailCommunications { get; init; }
    public required string IpAddress { get; init; }
    public required DateTime LastSeen { get; init; }
    public required bool IsActive { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required string UserStatusId { get; init; }
    public required string RoleId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? ModifiedBy { get; init; }
    public StatusSummaryDto? Status { get; init; }
    public RoleSummaryDto? Roles { get; init; }
    public required IReadOnlyList<string?> IpAddresses { get; init; }
}

public sealed record UserDetailsResponseDto : ResponseDto
{
    public UserDetailsData? Data { get; init; }
}
