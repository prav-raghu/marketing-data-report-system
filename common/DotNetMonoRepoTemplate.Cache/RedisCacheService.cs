using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DotNetMonoRepoTemplate.Cache;

public sealed class RedisCacheService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public IDatabase? Database => _connection.IsConnected ? _connection.GetDatabase() : null;

    private async Task<T> SafeExecuteAsync<T>(Func<IDatabase, Task<T>> operation, T fallback)
    {
        var database = Database;
        if (database is null)
        {
            return fallback;
        }
        try
        {
            return await operation(database);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis operation failed, using fallback");
            return fallback;
        }
    }

    private async Task SafeExecuteAsync(Func<IDatabase, Task> operation)
    {
        var database = Database;
        if (database is null)
        {
            return;
        }
        try
        {
            await operation(database);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis operation failed, using fallback");
        }
    }

    public Task SetUserOnlineAsync(string userId, string socketId, int ttlSeconds = 86400) =>
        SafeExecuteAsync(async db =>
        {
            var payload = JsonSerializer.Serialize(
                new UserPresenceCacheEntry(socketId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await db.StringSetAsync($"presence:{userId}", payload, TimeSpan.FromSeconds(ttlSeconds));
            await db.SetAddAsync("online_users", userId);
        });

    public Task SetUserOfflineAsync(string userId) =>
        SafeExecuteAsync(async db =>
        {
            await db.KeyDeleteAsync($"presence:{userId}");
            await db.SetRemoveAsync("online_users", userId);
        });

    public Task<bool> IsUserOnlineAsync(string userId) =>
        SafeExecuteAsync(db => db.KeyExistsAsync($"presence:{userId}"), false);

    public Task<string[]> GetOnlineUsersAsync() =>
        SafeExecuteAsync(
            async db =>
            {
                var members = await db.SetMembersAsync("online_users");
                return Array.ConvertAll(members, member => (string)member!);
            },
            Array.Empty<string>());

    public Task<UserPresenceCacheEntry?> GetUserPresenceAsync(string userId) =>
        SafeExecuteAsync(
            async db =>
            {
                var value = await db.StringGetAsync($"presence:{userId}");
                return value.HasValue ? JsonSerializer.Deserialize<UserPresenceCacheEntry>(value!) : null;
            },
            (UserPresenceCacheEntry?)null);

    public Task CacheConversationsAsync<T>(string userId, IReadOnlyList<T> conversations, int ttlSeconds = 300) =>
        SafeExecuteAsync(db =>
            db.StringSetAsync($"conversations:{userId}", JsonSerializer.Serialize(conversations), TimeSpan.FromSeconds(ttlSeconds)));

    public Task<IReadOnlyList<T>?> GetCachedConversationsAsync<T>(string userId) =>
        SafeExecuteAsync(
            async db =>
            {
                var value = await db.StringGetAsync($"conversations:{userId}");
                return value.HasValue ? JsonSerializer.Deserialize<IReadOnlyList<T>>(value!) : null;
            },
            (IReadOnlyList<T>?)null);

    public Task InvalidateConversationsAsync(string userId) =>
        SafeExecuteAsync(db => db.KeyDeleteAsync($"conversations:{userId}"));

    public Task CacheMessagesAsync<T>(string roomId, IReadOnlyList<T> messages, int ttlSeconds = 600) =>
        SafeExecuteAsync(db =>
            db.StringSetAsync($"messages:{roomId}", JsonSerializer.Serialize(messages), TimeSpan.FromSeconds(ttlSeconds)));

    public Task<IReadOnlyList<T>?> GetCachedMessagesAsync<T>(string roomId) =>
        SafeExecuteAsync(
            async db =>
            {
                var value = await db.StringGetAsync($"messages:{roomId}");
                return value.HasValue ? JsonSerializer.Deserialize<IReadOnlyList<T>>(value!) : null;
            },
            (IReadOnlyList<T>?)null);

    public Task InvalidateMessagesAsync(string roomId) =>
        SafeExecuteAsync(db => db.KeyDeleteAsync($"messages:{roomId}"));

    public Task CacheUserAsync<T>(string userId, T userData, int ttlSeconds = 3600) =>
        SafeExecuteAsync(db =>
            db.StringSetAsync($"user:{userId}", JsonSerializer.Serialize(userData), TimeSpan.FromSeconds(ttlSeconds)));

    public Task<T?> GetCachedUserAsync<T>(string userId) =>
        SafeExecuteAsync(
            async db =>
            {
                var value = await db.StringGetAsync($"user:{userId}");
                return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
            },
            default(T));

    public async Task<IReadOnlyDictionary<string, T>> GetCachedUsersAsync<T>(IReadOnlyList<string> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, T>();
        }

        return await SafeExecuteAsync(
            async db =>
            {
                var keys = userIds.Select(id => (RedisKey)$"user:{id}").ToArray();
                var values = await db.StringGetAsync(keys);
                var result = new Dictionary<string, T>();
                for (var i = 0; i < values.Length; i++)
                {
                    if (!values[i].HasValue)
                    {
                        continue;
                    }
                    var deserialized = JsonSerializer.Deserialize<T>(values[i]!);
                    if (deserialized is not null)
                    {
                        result[userIds[i]] = deserialized;
                    }
                }
                return (IReadOnlyDictionary<string, T>)result;
            },
            new Dictionary<string, T>());
    }
}
