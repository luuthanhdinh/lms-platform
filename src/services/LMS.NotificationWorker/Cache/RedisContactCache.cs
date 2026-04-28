using System.Text.Json;
using StackExchange.Redis;

namespace LMS.NotificationWorker.Cache;

public sealed class RedisContactCache : IContactCache
{
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromDays(90);
    private readonly IConnectionMultiplexer _redis;

    public RedisContactCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task SetAsync(Guid tenantId, Guid userId, ContactInfo contact, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(tenantId, userId);
        var json = JsonSerializer.Serialize(contact);
        await db.StringSetAsync(key, json, SlidingTtl);
    }

    public async Task<ContactInfo?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(tenantId, userId);
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return null;

        // Refresh TTL on read (sliding)
        await db.KeyExpireAsync(key, SlidingTtl);
        return JsonSerializer.Deserialize<ContactInfo>(value!);
    }

    private static string BuildKey(Guid tenantId, Guid userId) =>
        $"notif:user:{tenantId}:{userId}";
}
