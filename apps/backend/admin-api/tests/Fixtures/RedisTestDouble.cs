using System.Globalization;
using System.Net;
using Moq;
using StackExchange.Redis;

namespace AdminApi.Tests.Fixtures;

public static class RedisTestDouble
{
    public static Mock<IConnectionMultiplexer> Disconnected()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.IsConnected).Returns(false);
        return multiplexer;
    }
}

public sealed class ConnectedRedisTestDouble
{
    private readonly Dictionary<string, RedisValue> _store = new();

    public Mock<IDatabase> Database { get; } = new();

    public Mock<IConnectionMultiplexer> Multiplexer { get; } = new();

    public ConnectedRedisTestDouble()
    {
        Multiplexer.Setup(m => m.IsConnected).Returns(true);
        Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Database.Object);
        Multiplexer.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<EndPoint>());

        Database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? expiry, When when, CommandFlags flags) =>
            {
                _store[(string)key!] = value;
                return Task.FromResult(true);
            });

        Database
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, Expiration expiry, ValueCondition when, CommandFlags flags) =>
            {
                _store[(string)key!] = value;
                return Task.FromResult(true);
            });

        Database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags flags) =>
                Task.FromResult(_store.TryGetValue((string)key!, out var value) ? value : RedisValue.Null));

        Database
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags flags) => Task.FromResult(_store.ContainsKey((string)key!)));

        Database
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags flags) => Task.FromResult(_store.Remove((string)key!)));

        Database
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, long value, CommandFlags flags) =>
            {
                var stringKey = (string)key!;
                var current = _store.TryGetValue(stringKey, out var existing) && long.TryParse(existing.ToString(), out var parsed)
                    ? parsed
                    : 0;
                var next = current + value;
                _store[stringKey] = next.ToString(CultureInfo.InvariantCulture);
                return Task.FromResult(next);
            });

        Database
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    public void Seed(string key, string value) => _store[key] = value;

    public bool Contains(string key) => _store.ContainsKey(key);

    public void Remove(string key) => _store.Remove(key);
}
