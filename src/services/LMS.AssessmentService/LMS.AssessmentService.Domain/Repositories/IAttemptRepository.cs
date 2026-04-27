using LMS.AssessmentService.Domain.Entities;

namespace LMS.AssessmentService.Domain.Repositories;

public interface IAttemptRepository
{
    Task<AssessmentAttempt?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<int> CountByUserAsync(Guid tenantId, Guid userId, Guid assessmentId, CancellationToken ct = default);
    Task<IReadOnlyList<AssessmentAttempt>> ListByUserAsync(Guid tenantId, Guid userId, Guid assessmentId, CancellationToken ct = default);
    Task AddAsync(AssessmentAttempt attempt, CancellationToken ct = default);
}
