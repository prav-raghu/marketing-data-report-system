using Microsoft.EntityFrameworkCore;
using DotNetMonoRepoTemplate.Database;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Services;

public sealed record UserFilters
{
    public string? GenderId { get; init; }
    public int? MinAge { get; init; }
    public int? MaxAge { get; init; }
    public int Limit { get; init; } = 20;
    public int Offset { get; init; }
}

public sealed record UserSummary
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public int? Age { get; init; }
    public required DateTime LastSeen { get; init; }
}

public sealed record AuthorizedUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}

public sealed class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(
        UserFilters filters,
        string? loggedInUserId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.Where(u => u.IsActive);

        if (!string.IsNullOrEmpty(loggedInUserId))
        {
            query = query.Where(u => u.Id != loggedInUserId);
        }
        if (!string.IsNullOrEmpty(filters.GenderId))
        {
            query = query.Where(u => u.GenderId == filters.GenderId);
        }
        if (filters.MinAge.HasValue)
        {
            query = query.Where(u => u.Age >= filters.MinAge.Value);
        }
        if (filters.MaxAge.HasValue)
        {
            query = query.Where(u => u.Age <= filters.MaxAge.Value);
        }

        return await query
            .OrderByDescending(u => u.LastSeen)
            .Skip(filters.Offset)
            .Take(filters.Limit)
            .Select(u => new UserSummary { Id = u.Id, Username = u.Username, Age = u.Age, LastSeen = u.LastSeen })
            .ToListAsync(cancellationToken);
    }

    public Task<UserSummary?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new UserSummary { Id = u.Id, Username = u.Username, Age = u.Age, LastSeen = u.LastSeen })
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<AuthorizedUser?> GetAuthorizedUserByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Users
            .Where(u => u.Id == userId && u.IsActive && u.Roles != null && u.Roles.Name == RoleName.ChatUser)
            .Select(u => new AuthorizedUser { Id = u.Id, Username = u.Username, Email = u.Email, Role = u.Roles!.Name })
            .FirstOrDefaultAsync(cancellationToken)!;
}
