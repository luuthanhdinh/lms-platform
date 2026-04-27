using LMS.ContentService.Domain.Abstractions;

namespace LMS.ContentService.Api.Auth;

public sealed class HeaderTenantContext : ITenantContext
{
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string[] Roles { get; }

    public HeaderTenantContext(IHttpContextAccessor accessor)
    {
        var headers = accessor.HttpContext?.Request.Headers;
        TenantId = Guid.TryParse(headers?["X-Tenant-Id"].FirstOrDefault(), out var t) ? t : Guid.Empty;
        UserId = Guid.TryParse(headers?["X-User-Id"].FirstOrDefault(), out var u) ? u : Guid.Empty;
        Roles = headers?["X-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim().ToLowerInvariant())
            .ToArray() ?? [];
    }
}
