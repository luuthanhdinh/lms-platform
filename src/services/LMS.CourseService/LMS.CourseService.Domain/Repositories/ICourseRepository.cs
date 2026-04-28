using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Enums;

namespace LMS.CourseService.Domain.Repositories;

public interface ICourseRepository
{
    Task<Course?> FindByIdAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task<Course?> FindByIdWithSectionsAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task<(IReadOnlyList<Course> Items, int Total)> ListPublishedAsync(
        Guid tenantId, string? category, string? tag, DifficultyLevel? difficulty,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
}
