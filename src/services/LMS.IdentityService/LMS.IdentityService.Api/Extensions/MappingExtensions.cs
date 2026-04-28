using LMS.IdentityService.Api.Models;
using LMS.IdentityService.Domain.Entities;

namespace LMS.IdentityService.Api.Extensions;

public static class MappingExtensions
{
    public static UserProfileDto ToDto(this UserProfile p) => new(
        p.Id, p.TenantId, p.KeycloakId, p.Email, p.DisplayName,
        p.AvatarUrl, p.Bio, p.Timezone, p.Language,
        p.Role.ToString().ToLowerInvariant(), p.IsActive, p.CreatedAt, p.UpdatedAt);

    public static UserSummaryDto ToSummaryDto(this UserProfile p) => new(
        p.Id, p.Email, p.DisplayName,
        p.Role.ToString().ToLowerInvariant(), p.IsActive, p.CreatedAt);

    public static UserInviteDto ToDto(this UserInvite i) => new(
        i.Id, i.Email, i.Role.ToString().ToLowerInvariant(), i.Token,
        i.ExpiresAt, i.IsAccepted, i.CreatedAt);

    public static TenantConfigDto ToDto(this TenantConfig c) => new(
        c.Id, c.Name, c.LogoUrl, c.Timezone,
        c.AllowedEmailDomains, c.Plan.ToString().ToLowerInvariant(),
        c.CreatedAt, c.UpdatedAt);
}
