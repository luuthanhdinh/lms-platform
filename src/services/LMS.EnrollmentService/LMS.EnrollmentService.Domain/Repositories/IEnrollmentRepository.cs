using LMS.EnrollmentService.Domain.Entities;
using LMS.EnrollmentService.Domain.Enums;

namespace LMS.EnrollmentService.Domain.Repositories;

public interface IEnrollmentRepository
{
    Task<Enrollment?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Enrollment> Items, int Total)> ListByUserAsync(Guid tenantId, Guid userId, EnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<(IReadOnlyList<Enrollment> Items, int Total)> ListAllAsync(Guid tenantId, EnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<(IReadOnlyList<Enrollment> Items, int Total)> ListByCourseAsync(Guid tenantId, Guid courseId, EnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<(int ActiveCount, int TotalCount)> CountByCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task<Enrollment?> FindActiveAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default);
    Task<IReadOnlyList<Enrollment>> FindActiveEnrollmentsForCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task AddAsync(Enrollment enrollment, CancellationToken ct = default);
    Task UpdateAsync(Enrollment enrollment, CancellationToken ct = default);
}
