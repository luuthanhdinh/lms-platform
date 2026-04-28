using LMS.IdentityService.Domain.Abstractions;

namespace LMS.IdentityService.Api.Auth;

public sealed class HeaderTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;

    public HeaderTenantContext(IHttpContextAccessor http) => _http = http;

    private HttpContext Ctx => _http.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context");

    public Guid TenantId =>
        Guid.TryParse(Ctx.Request.Headers["X-Tenant-Id"], out var t) ? t : Guid.Empty;

    public Guid UserId =>
        Guid.TryParse(Ctx.Request.Headers["X-User-Id"], out var u) ? u : Guid.Empty;

    public string[] Roles =>
        Ctx.Request.Headers["X-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
