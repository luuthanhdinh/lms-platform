using LMS.ContentService.Api.Auth;
using LMS.ContentService.Domain.Abstractions;
using Xunit;

namespace LMS.IntegrationTests.Content.Unit;

[Trait("Category", "Content")]
public class ContentAuthorizationTests
{
    private static ITenantContext MakeCtx(Guid? tenantId = null, Guid? userId = null, string[]? roles = null) =>
        new FakeTenantContext(
            tenantId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            roles ?? []);

    [Fact]
    public void IsAuthenticated_BothSet_ReturnsTrue()
    {
        var ctx = MakeCtx(tenantId: Guid.NewGuid(), userId: Guid.NewGuid());

        Assert.True(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsAuthenticated_EmptyTenantId_ReturnsFalse()
    {
        var ctx = MakeCtx(tenantId: Guid.Empty);

        Assert.False(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsAuthenticated_EmptyUserId_ReturnsFalse()
    {
        var ctx = MakeCtx(userId: Guid.Empty);

        Assert.False(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsInstructorOrAdmin_Instructor_ReturnsTrue()
    {
        var ctx = MakeCtx(roles: ["instructor"]);

        Assert.True(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    [Fact]
    public void IsInstructorOrAdmin_Admin_ReturnsTrue()
    {
        var ctx = MakeCtx(roles: ["admin"]);

        Assert.True(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    [Fact]
    public void IsInstructorOrAdmin_OrgAdmin_ReturnsTrue()
    {
        var ctx = MakeCtx(roles: ["org-admin"]);

        Assert.True(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    [Fact]
    public void IsInstructorOrAdmin_Student_ReturnsFalse()
    {
        var ctx = MakeCtx(roles: ["student"]);

        Assert.False(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    [Fact]
    public void IsAdmin_Admin_ReturnsTrue()
    {
        var ctx = MakeCtx(roles: ["admin"]);

        Assert.True(AuthorizationHelpers.IsAdmin(ctx));
    }

    [Fact]
    public void IsAdmin_Instructor_ReturnsFalse()
    {
        var ctx = MakeCtx(roles: ["instructor"]);

        Assert.False(AuthorizationHelpers.IsAdmin(ctx));
    }

    private sealed record FakeTenantContext(Guid TenantId, Guid UserId, string[] Roles) : ITenantContext;
}
