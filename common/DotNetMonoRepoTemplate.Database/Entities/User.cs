namespace DotNetMonoRepoTemplate.Database.Entities;

public sealed class User : AuditableEntity
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public string? Avatar { get; set; }
    public string? GenderId { get; set; }
    public int? Age { get; set; }
    public bool AcceptTermsAndConditions { get; set; }
    public bool AllowEmailCommunications { get; set; }
    public required string IpAddress { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? AuthHash { get; set; }
    public DateTime? AuthHashExpiration { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public required string UserStatusId { get; set; }
    public UserStatus? Status { get; set; }
    public required string RoleId { get; set; }
    public Role? Roles { get; set; }
}
