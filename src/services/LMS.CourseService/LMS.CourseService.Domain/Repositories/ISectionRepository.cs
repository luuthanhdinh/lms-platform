using LMS.CourseService.Domain.Entities;

namespace LMS.CourseService.Domain.Repositories;

public interface ISectionRepository
{
    Task<CourseSection?> FindByIdAsync(Guid tenantId, Guid sectionId, CancellationToken ct = default);
    Task AddAsync(CourseSection section, CancellationToken ct = default);
    Task DeleteAsync(CourseSection section, CancellationToken ct = default);
}
