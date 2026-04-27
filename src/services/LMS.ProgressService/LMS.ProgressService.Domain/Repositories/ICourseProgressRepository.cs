using LMS.ProgressService.Domain.Entities;

namespace LMS.ProgressService.Domain.Repositories;

public interface ICourseProgressRepository
{
    Task<CourseProgress?> FindAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseProgress>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseProgress>> ListByCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task AddAsync(CourseProgress progress, CancellationToken ct = default);
    Task UpdateAsync(CourseProgress progress, CancellationToken ct = default);
}
