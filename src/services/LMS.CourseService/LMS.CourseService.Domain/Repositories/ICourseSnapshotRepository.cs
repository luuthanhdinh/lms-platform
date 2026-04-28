using LMS.CourseService.Domain.Entities;

namespace LMS.CourseService.Domain.Repositories;

public interface ICourseSnapshotRepository
{
    Task AddAsync(CourseSnapshot snapshot, CancellationToken ct = default);
}
