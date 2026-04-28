using LMS.AssessmentService.Domain.Abstractions;
using Microsoft.AspNetCore.Http;

namespace LMS.AssessmentService.Infrastructure.Auth;

public sealed class HeaderTenantContext : ITenantContext
{
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string[] Roles { get; }

    public HeaderTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;

        TenantId = Guid.TryParse(headers?["X-Tenant-Id"].FirstOrDefault(), out var tid) ? tid : Guid.Empty;
        UserId = Guid.TryParse(headers?["X-User-Id"].FirstOrDefault(), out var uid) ? uid : Guid.Empty;

        var rolesHeader = headers?["X-Roles"].FirstOrDefault();
        Roles = string.IsNullOrWhiteSpace(rolesHeader)
            ? []
            : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(r => r.ToLowerInvariant())
                         .ToArray();
    }
}
