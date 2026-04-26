using LMS.IdentityService.Api.Auth;
using LMS.IdentityService.Domain.Abstractions;
using Xunit;

namespace LMS.IntegrationTests.Identity.Unit;

[Trait("Category", "Identity")]
public class AuthorizationHelpersTests
{
    private static ITenantContext MakeCtx(Guid? tenantId = null, Guid? userId = null, string[]? roles = null) =>
        new StubTenantContext(
            tenantId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            roles ?? []);

    [Theory]
    [InlineData("admin")]
    [InlineData("org-admin")]
    public void IsAdminOrOrgAdmin_ReturnsTrue_ForAdminRoles(string role)
    {
        var ctx = MakeCtx(roles: [role]);
        Assert.True(AuthorizationHelpers.IsAdminOrOrgAdmin(ctx));
    }

    [Theory]
    [InlineData("student")]
    [InlineData("instructor")]
    public void IsAdminOrOrgAdmin_ReturnsFalse_ForNonAdminRoles(string role)
    {
        var ctx = MakeCtx(roles: [role]);
        Assert.False(AuthorizationHelpers.IsAdminOrOrgAdmin(ctx));
    }

    [Fact]
    public void IsAdminOrOrgAdmin_ReturnsFalse_WhenRolesEmpty()
    {
        var ctx = MakeCtx(roles: []);
        Assert.False(AuthorizationHelpers.IsAdminOrOrgAdmin(ctx));
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenTenantIdEmpty()
    {
        var ctx = MakeCtx(tenantId: Guid.Empty);
        Assert.False(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenUserIdEmpty()
    {
        var ctx = MakeCtx(userId: Guid.Empty);
        Assert.False(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenBothEmpty()
    {
        var ctx = MakeCtx(tenantId: Guid.Empty, userId: Guid.Empty);
        Assert.False(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    [Fact]
    public void IsAuthenticated_ReturnsTrue_WhenBothPresent()
    {
        var ctx = MakeCtx(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(AuthorizationHelpers.IsAuthenticated(ctx));
    }

    private sealed record StubTenantContext(Guid TenantId, Guid UserId, string[] Roles) : ITenantContext;
}
