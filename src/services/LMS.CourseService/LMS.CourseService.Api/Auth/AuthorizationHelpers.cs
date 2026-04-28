using LMS.CourseService.Domain.Abstractions;

namespace LMS.CourseService.Api.Auth;

public static class AuthorizationHelpers
{
    public static bool IsAuthenticated(ITenantContext ctx) =>
        ctx.TenantId != Guid.Empty && ctx.UserId != Guid.Empty;

    public static bool IsInstructorOrAdmin(ITenantContext ctx) =>
        ctx.Roles.Contains("instructor") || ctx.Roles.Contains("admin") || ctx.Roles.Contains("org-admin");

    public static bool IsOwnerOrAdmin(ITenantContext ctx, Guid instructorId) =>
        ctx.UserId == instructorId || ctx.Roles.Contains("admin") || ctx.Roles.Contains("org-admin");

    public static bool IsAdmin(ITenantContext ctx) =>
        ctx.Roles.Contains("admin") || ctx.Roles.Contains("org-admin");
}
