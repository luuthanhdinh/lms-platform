using StackExchange.Redis;

namespace LMS.NotificationWorker.Idempotency;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var fullKey = $"notif:dedupe:{key}";
        return await db.StringSetAsync(fullKey, "1", ttl, When.NotExists);
    }
}
