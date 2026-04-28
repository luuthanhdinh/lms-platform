using LMS.IdentityService.Domain.Entities;

namespace LMS.IdentityService.Domain.Repositories;

public interface ITenantConfigRepository
{
    Task<TenantConfig?> FindAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantConfig config, CancellationToken ct = default);
}
