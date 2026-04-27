using LMS.AssessmentService.Domain.Entities;

namespace LMS.AssessmentService.Domain.Repositories;

public interface IAssessmentRepository
{
    Task<Assessment?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Assessment> Items, int Total)> ListByCourseAsync(Guid tenantId, Guid courseId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Assessment>> FindActiveByCourseAsync(Guid tenantId, Guid courseId, CancellationToken ct = default);
    Task AddAsync(Assessment assessment, CancellationToken ct = default);
    Task UpdateAsync(Assessment assessment, CancellationToken ct = default);
}
