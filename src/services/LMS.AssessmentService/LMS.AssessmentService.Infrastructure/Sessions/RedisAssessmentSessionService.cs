using LMS.AssessmentService.Domain.Sessions;
using StackExchange.Redis;
using System.Text.Json;

namespace LMS.AssessmentService.Infrastructure.Sessions;

public sealed class RedisAssessmentSessionService : IAssessmentSessionService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisAssessmentSessionService(IConnectionMultiplexer redis) => _redis = redis;

    private static string Key(Guid sessionId) => $"assessment:session:{sessionId}";

    public async Task<AssessmentSession> CreateAsync(AssessmentSession session, int ttlSeconds, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(session);
        await db.StringSetAsync(Key(session.SessionId), json, TimeSpan.FromSeconds(ttlSeconds));
        return session;
    }

    public async Task<AssessmentSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(Key(sessionId));
        if (!value.HasValue)
            return null;

        return JsonSerializer.Deserialize<AssessmentSession>(value!);
    }

    public async Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(Key(sessionId));
    }
}
