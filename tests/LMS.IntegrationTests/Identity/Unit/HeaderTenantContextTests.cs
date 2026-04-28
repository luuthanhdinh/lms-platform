using LMS.IdentityService.Api.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LMS.IntegrationTests.Identity.Unit;

[Trait("Category", "Identity")]
public class HeaderTenantContextTests
{
    private static IHttpContextAccessor MakeAccessor(
        string? userId = null, string? tenantId = null, string? roles = null)
    {
        var ctx = new DefaultHttpContext();
        if (userId != null) ctx.Request.Headers["X-User-Id"] = userId;
        if (tenantId != null) ctx.Request.Headers["X-Tenant-Id"] = tenantId;
        if (roles != null) ctx.Request.Headers["X-Roles"] = roles;
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return accessor;
    }

    [Fact]
    public void TenantId_ParsedCorrectly_FromHeader()
    {
        var id = Guid.NewGuid();
        var ctx = new HeaderTenantContext(MakeAccessor(tenantId: id.ToString()));
        Assert.Equal(id, ctx.TenantId);
    }

    [Fact]
    public void TenantId_ReturnsEmpty_WhenHeaderMissing()
    {
        var ctx = new HeaderTenantContext(MakeAccessor());
        Assert.Equal(Guid.Empty, ctx.TenantId);
    }

    [Fact]
    public void UserId_ParsedCorrectly_FromHeader()
    {
        var id = Guid.NewGuid();
        var ctx = new HeaderTenantContext(MakeAccessor(userId: id.ToString()));
        Assert.Equal(id, ctx.UserId);
    }

    [Fact]
    public void Roles_SplitOnComma_WithTrimming()
    {
        var ctx = new HeaderTenantContext(MakeAccessor(roles: "student,instructor"));
        Assert.Equal(["student", "instructor"], ctx.Roles);
    }

    [Fact]
    public void Roles_Empty_WhenHeaderMissing()
    {
        var ctx = new HeaderTenantContext(MakeAccessor());
        Assert.Empty(ctx.Roles);
    }

    [Fact]
    public void TenantId_ReturnsEmpty_WhenHeaderIsInvalidGuid()
    {
        var ctx = new HeaderTenantContext(MakeAccessor(tenantId: "not-a-guid"));
        Assert.Equal(Guid.Empty, ctx.TenantId);
    }

    [Fact]
    public void UserId_ReturnsEmpty_WhenHeaderIsInvalidGuid()
    {
        var ctx = new HeaderTenantContext(MakeAccessor(userId: "not-a-guid"));
        Assert.Equal(Guid.Empty, ctx.UserId);
    }

    [Fact]
    public void Roles_HandlesWhitespaceAroundCommas()
    {
        var ctx = new HeaderTenantContext(MakeAccessor(roles: "admin , instructor , student"));
        Assert.Equal(["admin", "instructor", "student"], ctx.Roles);
    }
}
