using LMS.CertificateService.Domain.Entities;
using LMS.CertificateService.Domain.Repositories;
using LMS.CertificateService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.CertificateService.Infrastructure.Repositories;

public sealed class CertificateRepository : ICertificateRepository
{
    private readonly CertificateDbContext _db;

    public CertificateRepository(CertificateDbContext db)
    {
        _db = db;
    }

    public Task<Certificate?> FindAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Certificates
              .Where(c => c.TenantId == tenantId && c.Id == id)
              .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Anonymous verify endpoint — bypasses tenant query filter.
    /// IgnoreQueryFilters() justified: public verification is tenant-agnostic by design (contracts.md §7).
    /// </summary>
    public Task<Certificate?> FindByVerificationCodeAsync(Guid code, CancellationToken ct = default)
        => _db.Certificates
              .IgnoreQueryFilters()
              .Where(c => c.VerificationCode == code)
              .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Used by consumers (no HttpContext) — explicit tenantId predicate, bypasses global filter.
    /// IgnoreQueryFilters() justified: consumer scope has no HTTP context; tenantId comes from message.
    /// </summary>
    public Task<Certificate?> FindByUserCourseAsync(Guid tenantId, Guid userId, Guid courseId, CancellationToken ct = default)
        => _db.Certificates
              .IgnoreQueryFilters()
              .Where(c => c.TenantId == tenantId && c.UserId == userId && c.CourseId == courseId && c.RevokedAt == null)
              .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Certificate>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.Certificates
                    .Where(c => c.TenantId == tenantId && c.UserId == userId)
                    .OrderByDescending(c => c.IssuedAt)
                    .ToListAsync(ct);

    /// <summary>Stage only — caller owns the transaction and calls SaveChangesAsync.</summary>
    public async Task AddAsync(Certificate cert, CancellationToken ct = default)
    {
        await _db.Certificates.AddAsync(cert, ct);
    }

    /// <summary>Stage only — caller owns the transaction and calls SaveChangesAsync.</summary>
    public Task UpdateAsync(Certificate cert, CancellationToken ct = default)
    {
        _db.Certificates.Update(cert);
        return Task.CompletedTask;
    }
}
