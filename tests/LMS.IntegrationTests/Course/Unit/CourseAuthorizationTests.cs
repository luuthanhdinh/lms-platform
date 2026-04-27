using LMS.CourseService.Api.Auth;
using LMS.CourseService.Domain.Abstractions;
using Xunit;

namespace LMS.IntegrationTests.Course.Unit;

[Trait("Category", "Course")]
public class CourseAuthorizationTests
{
    private static ITenantContext MakeCtx(Guid? tenantId = null, Guid? userId = null, string[]? roles = null) =>
        new FakeTenantContext(
            tenantId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            roles ?? []);

    // IsOwnerOrAdmin tests

    [Fact]
    public void IsOwnerOrAdmin_Owner_ReturnsTrue()
    {
        var instructorId = Guid.NewGuid();
        var ctx = MakeCtx(userId: instructorId, roles: ["instructor"]);

        Assert.True(AuthorizationHelpers.IsOwnerOrAdmin(ctx, instructorId));
    }

    [Fact]
    public void IsOwnerOrAdmin_Admin_ReturnsTrue()
    {
        var instructorId = Guid.NewGuid();
        var ctx = MakeCtx(roles: ["admin"]);

        Assert.True(AuthorizationHelpers.IsOwnerOrAdmin(ctx, instructorId));
    }

    [Fact]
    public void IsOwnerOrAdmin_OrgAdmin_ReturnsTrue()
    {
        var instructorId = Guid.NewGuid();
        var ctx = MakeCtx(roles: ["org-admin"]);

        Assert.True(AuthorizationHelpers.IsOwnerOrAdmin(ctx, instructorId));
    }

    [Fact]
    public void IsOwnerOrAdmin_OtherUser_ReturnsFalse()
    {
        var instructorId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var ctx = MakeCtx(userId: differentUserId, roles: ["student"]);

        Assert.False(AuthorizationHelpers.IsOwnerOrAdmin(ctx, instructorId));
    }

    [Fact]
    public void IsOwnerOrAdmin_OtherUser_NoRole_ReturnsFalse()
    {
        var instructorId = Guid.NewGuid();
        var ctx = MakeCtx(userId: Guid.NewGuid(), roles: []);

        Assert.False(AuthorizationHelpers.IsOwnerOrAdmin(ctx, instructorId));
    }

    // IsInstructorOrAdmin tests

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
    public void IsInstructorOrAdmin_NoRole_ReturnsFalse()
    {
        var ctx = MakeCtx(roles: []);

        Assert.False(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    [Fact]
    public void IsInstructorOrAdmin_Student_ReturnsFalse()
    {
        var ctx = MakeCtx(roles: ["student"]);

        Assert.False(AuthorizationHelpers.IsInstructorOrAdmin(ctx));
    }

    // IsAuthenticated tests

    [Fact]
    public void IsAuthenticated_BothPresent_ReturnsTrue()
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

    private sealed record FakeTenantContext(Guid TenantId, Guid UserId, string[] Roles) : ITenantContext;
}
