namespace LMS.NotificationWorker.Cache;

public interface IContactCache
{
    Task SetAsync(Guid tenantId, Guid userId, ContactInfo contact, CancellationToken ct = default);
    Task<ContactInfo?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

public sealed record ContactInfo(string Email, string FullName);
