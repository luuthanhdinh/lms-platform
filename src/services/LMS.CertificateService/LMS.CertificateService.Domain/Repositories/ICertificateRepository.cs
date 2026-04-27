using LMS.CertificateService.Domain.Entities;

namespace LMS.CertificateService.Domain.Repositories;

public interface ICertificateRepository
{
    Task<Certificate?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Anonymous verify — no tenant filter applied.</summary>
    Task<Certificate?> FindByVerificationCodeAsync(Guid code, CancellationToken ct = default);

    /// <summary>Used for idempotency check — explicit tenantId predicate, ignores global filter.</summary>
    Task<Certificate?> FindByUserCourseAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default);

    Task<IReadOnlyList<Certificate>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Stage only — does NOT call SaveChangesAsync.</summary>
    Task AddAsync(Certificate cert, CancellationToken ct = default);

    /// <summary>Stage only — does NOT call SaveChangesAsync.</summary>
    Task UpdateAsync(Certificate cert, CancellationToken ct = default);
}
