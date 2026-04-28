using LMS.CertificateService.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace LMS.CertificateService.Infrastructure.Auth;

public sealed class HeaderTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : Guid.Empty;
        }
    }
}
