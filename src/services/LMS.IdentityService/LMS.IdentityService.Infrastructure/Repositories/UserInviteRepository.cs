using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.IdentityService.Infrastructure.Repositories;

public sealed class UserInviteRepository : IUserInviteRepository
{
    private readonly IdentityDbContext _db;

    public UserInviteRepository(IdentityDbContext db) => _db = db;

    public Task<UserInvite?> FindPendingByEmailAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.UserInvites
            .Where(x => x.TenantId == tenantId && x.Email == email && !x.IsAccepted && x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasPendingInviteAsync(Guid tenantId, string email, CancellationToken ct = default) =>
        _db.UserInvites.AnyAsync(x => x.TenantId == tenantId && x.Email == email && !x.IsAccepted && x.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task AddAsync(UserInvite invite, CancellationToken ct = default) =>
        await _db.UserInvites.AddAsync(invite, ct);
}
