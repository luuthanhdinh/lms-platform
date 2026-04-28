using LMS.ProgressService.Domain.Entities;

namespace LMS.ProgressService.Domain.Repositories;

public interface ILessonProgressRepository
{
    Task<LessonProgress?> FindAsync(Guid tenantId, Guid userId, Guid lessonId, CancellationToken ct = default);
    Task<IReadOnlyList<LessonProgress>> ListByCourseAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default);
    Task AddAsync(LessonProgress progress, CancellationToken ct = default);
    Task UpdateAsync(LessonProgress progress, CancellationToken ct = default);
}
