using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Enums;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.IdentityService.Infrastructure.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly IdentityDbContext _db;

    public UserProfileRepository(IdentityDbContext db) => _db = db;

    public Task<UserProfile?> FindByKeycloakIdAsync(Guid tenantId, string keycloakId, CancellationToken ct = default) =>
        _db.UserProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeycloakId == keycloakId, ct);

    public Task<UserProfile?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.UserProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public async Task<(IReadOnlyList<UserProfile> Items, int Total)> ListAsync(
        Guid tenantId, string? role, bool? active, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.UserProfiles.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrEmpty(role) &&
            Enum.TryParse<UserRole>(role.Replace("-", ""), true, out var roleEnum))
            query = query.Where(x => x.Role == roleEnum);

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(x =>
                x.DisplayName.Contains(search) || x.Email.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(UserProfile profile, CancellationToken ct = default) =>
        await _db.UserProfiles.AddAsync(profile, ct);

    public Task<bool> ExistsAsync(Guid tenantId, string keycloakId, CancellationToken ct = default) =>
        _db.UserProfiles.AnyAsync(x => x.TenantId == tenantId && x.KeycloakId == keycloakId, ct);
}
