using LMS.IdentityService.Api.Extensions;
using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Enums;
using Xunit;

namespace LMS.IntegrationTests.Identity.Unit;

[Trait("Category", "Identity")]
public class MappingExtensionsTests
{
    [Fact]
    public void UserProfile_ToDto_MapsAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            KeycloakId = "keycloak-sub-123",
            Email = "test@example.com",
            DisplayName = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            Bio = "Hello",
            Timezone = "Asia/Ho_Chi_Minh",
            Language = "vi",
            Role = UserRole.Instructor,
            IsActive = true,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now,
        };

        var dto = profile.ToDto();

        Assert.Equal(profile.Id, dto.Id);
        Assert.Equal(profile.TenantId, dto.TenantId);
        Assert.Equal(profile.KeycloakId, dto.KeycloakId);
        Assert.Equal(profile.Email, dto.Email);
        Assert.Equal(profile.DisplayName, dto.DisplayName);
        Assert.Equal("instructor", dto.Role);
        Assert.True(dto.IsActive);
        Assert.Equal(profile.CreatedAt, dto.CreatedAt);
        Assert.Equal(profile.UpdatedAt, dto.UpdatedAt);
    }

    [Fact]
    public void UserProfile_ToDto_OrgAdmin_MapsRoleAsOrgadmin()
    {
        // OrgAdmin.ToString() = "OrgAdmin", ToLowerInvariant() = "orgadmin" (no hyphen)
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Role = UserRole.OrgAdmin,
        };

        var dto = profile.ToDto();
        Assert.Equal("orgadmin", dto.Role);
    }

    [Theory]
    [InlineData(UserRole.Student, "student")]
    [InlineData(UserRole.Instructor, "instructor")]
    [InlineData(UserRole.Admin, "admin")]
    [InlineData(UserRole.OrgAdmin, "orgadmin")]
    public void UserProfile_ToDto_AllRoles_AreLowercaseInvariant(UserRole role, string expected)
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Role = role,
        };

        var dto = profile.ToDto();
        Assert.Equal(expected, dto.Role);
    }

    [Fact]
    public void UserProfile_ToSummaryDto_MapsFields()
    {
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "summary@example.com",
            DisplayName = "Summary User",
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var dto = profile.ToSummaryDto();

        Assert.Equal(profile.Id, dto.Id);
        Assert.Equal(profile.Email, dto.Email);
        Assert.Equal(profile.DisplayName, dto.DisplayName);
        Assert.Equal("student", dto.Role);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public void UserRole_ToString_IsLowercaseInvariant()
    {
        Assert.Equal("student", UserRole.Student.ToString().ToLowerInvariant());
        Assert.Equal("orgadmin", UserRole.OrgAdmin.ToString().ToLowerInvariant());
        Assert.Equal("admin", UserRole.Admin.ToString().ToLowerInvariant());
        Assert.Equal("instructor", UserRole.Instructor.ToString().ToLowerInvariant());
    }
}
