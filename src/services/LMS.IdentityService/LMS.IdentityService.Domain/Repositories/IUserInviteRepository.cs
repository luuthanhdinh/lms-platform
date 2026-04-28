using LMS.IdentityService.Domain.Entities;

namespace LMS.IdentityService.Domain.Repositories;

public interface IUserInviteRepository
{
    Task<UserInvite?> FindPendingByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<bool> HasPendingInviteAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task AddAsync(UserInvite invite, CancellationToken ct = default);
}
