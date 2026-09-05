using StackExchange.Redis;

namespace IngestionApi.RateLimiting;

public sealed class RedisRateLimiter : IRateLimiter
{
    private const string AcquireScript = @"
local current = redis.call('INCR', KEYS[1])
if current == 1 then
  redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
if current > tonumber(ARGV[1]) then
  redis.call('DECR', KEYS[1])
  return 0
end
return 1";

    private readonly IConnectionMultiplexer _redis;
    private readonly VendorRateLimiterOptions _options;
    private readonly TimeProvider _timeProvider;

    public RedisRateLimiter(IConnectionMultiplexer redis, VendorRateLimiterOptions options, TimeProvider timeProvider)
    {
        _redis = redis;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task AcquireAsync(string partitionKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        var deadline = _timeProvider.GetUtcNow() + _options.AcquireTimeout;
        var database = _redis.GetDatabase();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var granted = await database.ScriptEvaluateAsync(
                AcquireScript,
                [BuildKey(partitionKey)],
                [_options.PermitsPerWindow, (long)_options.Window.TotalMilliseconds]).ConfigureAwait(false);

            if ((int)granted == 1)
            {
                return;
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                throw new RateLimitTimeoutException(partitionKey, _options.AcquireTimeout);
            }

            await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private RedisKey BuildKey(string partitionKey)
    {
        var window = _timeProvider.GetUtcNow().UtcDateTime.Ticks / _options.Window.Ticks;
        return new RedisKey($"ratelimit:{partitionKey}:{window}");
    }
}
