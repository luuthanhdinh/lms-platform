using LMS.IdentityService.Domain.Entities;

namespace LMS.IdentityService.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> FindByKeycloakIdAsync(Guid tenantId, string keycloakId, CancellationToken ct = default);
    Task<UserProfile?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<UserProfile> Items, int Total)> ListAsync(
        Guid tenantId, string? role, bool? active, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(UserProfile profile, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, string keycloakId, CancellationToken ct = default);
}
