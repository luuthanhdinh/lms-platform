using LMS.CertificateService.Domain.Interfaces;

namespace LMS.CertificateService.Infrastructure.Data;

public sealed class EfUnitOfWork(CertificateDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
