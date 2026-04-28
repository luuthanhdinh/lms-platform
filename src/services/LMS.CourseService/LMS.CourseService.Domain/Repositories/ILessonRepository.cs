using LMS.CourseService.Domain.Entities;

namespace LMS.CourseService.Domain.Repositories;

public interface ILessonRepository
{
    Task<CourseLesson?> FindByIdAsync(Guid tenantId, Guid lessonId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseLesson>> FindByContentItemAsync(Guid tenantId, Guid contentItemId, CancellationToken ct = default);
    Task AddAsync(CourseLesson lesson, CancellationToken ct = default);
    Task DeleteAsync(CourseLesson lesson, CancellationToken ct = default);
}
