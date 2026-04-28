using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.IdentityService.Infrastructure.Repositories;

public sealed class TenantConfigRepository : ITenantConfigRepository
{
    private readonly IdentityDbContext _db;

    public TenantConfigRepository(IdentityDbContext db) => _db = db;

    public Task<TenantConfig?> FindAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.TenantConfigs.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

    public async Task AddAsync(TenantConfig config, CancellationToken ct = default) =>
        await _db.TenantConfigs.AddAsync(config, ct);
}
