using LMS.IdentityService.Domain.Abstractions;

namespace LMS.IdentityService.Api.Auth;

public static class AuthorizationHelpers
{
    public static bool IsAdminOrOrgAdmin(ITenantContext ctx) =>
        ctx.Roles.Any(r => r is "admin" or "org-admin");

    public static bool IsAuthenticated(ITenantContext ctx) =>
        ctx.TenantId != Guid.Empty && ctx.UserId != Guid.Empty;
}
