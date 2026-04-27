using LMS.CertificateService.Domain.Entities;

namespace LMS.CertificateService.Domain.Interfaces;

public interface ICertificatePdfGenerator
{
    Task<byte[]> GenerateAsync(Certificate cert, CancellationToken ct = default);
}
