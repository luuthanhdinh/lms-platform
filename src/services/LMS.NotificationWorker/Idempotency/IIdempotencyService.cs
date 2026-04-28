namespace LMS.NotificationWorker.Idempotency;

public interface IIdempotencyService
{
    /// <summary>Returns true if this key is new (claim succeeded); false if already processed.</summary>
    Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}
