using LMS.ProgressService.Domain.Abstractions;

namespace LMS.ProgressService.Api.Auth;

internal static class AuthorizationHelpers
{
    internal static bool IsAuthenticated(ITenantContext ctx) =>
        ctx.TenantId != Guid.Empty && ctx.UserId != Guid.Empty;
    internal static bool IsAdmin(ITenantContext ctx) =>
        ctx.Roles.Contains("admin") || ctx.Roles.Contains("org-admin");
    internal static bool IsInstructorOrAdmin(ITenantContext ctx) =>
        ctx.Roles.Contains("instructor") || IsAdmin(ctx);
}
