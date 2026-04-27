namespace LMS.CertificateService.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
}
