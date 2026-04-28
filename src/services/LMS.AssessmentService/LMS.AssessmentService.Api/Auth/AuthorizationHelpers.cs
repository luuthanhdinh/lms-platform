using LMS.AssessmentService.Domain.Abstractions;

namespace LMS.AssessmentService.Api.Auth;

internal static class AuthorizationHelpers
{
    internal static bool IsAuthenticated(ITenantContext ctx) =>
        ctx.TenantId != Guid.Empty && ctx.UserId != Guid.Empty;

    internal static bool IsAdmin(ITenantContext ctx) =>
        ctx.Roles.Contains("admin") || ctx.Roles.Contains("org-admin");

    internal static bool IsInstructor(ITenantContext ctx) =>
        ctx.Roles.Contains("instructor");

    internal static bool IsInstructorOrAdmin(ITenantContext ctx) =>
        IsInstructor(ctx) || IsAdmin(ctx);

    internal static bool IsStudent(ITenantContext ctx) =>
        ctx.Roles.Contains("student");
}
