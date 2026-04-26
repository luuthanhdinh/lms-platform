namespace LMS.IdentityService.Api.Models;

public record UserProfileDto(
    Guid Id, Guid TenantId, string KeycloakId, string Email, string DisplayName,
    string? AvatarUrl, string? Bio, string Timezone, string Language,
    string Role, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record UserSummaryDto(
    Guid Id, string Email, string DisplayName, string Role, bool IsActive,
    DateTimeOffset CreatedAt);

public record UpsertProfileRequest(
    string KeycloakId, string Email, string DisplayName,
    string? AvatarUrl, string? Timezone, string? Language);

public record UpdateProfileRequest(
    string DisplayName, string? AvatarUrl, string? Bio,
    string? Timezone, string? Language);

public record InviteUserRequest(string Email, string Role);

public record UserInviteDto(
    Guid Id, string Email, string Role, string Token,
    DateTimeOffset ExpiresAt, bool IsAccepted, DateTimeOffset CreatedAt);

public record TenantConfigDto(
    Guid Id, string Name, string? LogoUrl, string Timezone,
    string[] AllowedEmailDomains, string Plan,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record UpdateTenantRequest(
    string Name, string? LogoUrl, string? Timezone, string[]? AllowedEmailDomains);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
