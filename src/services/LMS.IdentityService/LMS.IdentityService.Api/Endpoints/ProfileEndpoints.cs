using LMS.Contracts.Identity;
using LMS.IdentityService.Api.Auth;
using LMS.IdentityService.Api.Extensions;
using LMS.IdentityService.Api.Models;
using static LMS.IdentityService.Api.Extensions.ValidationHelpers;
using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;
using MassTransit;

namespace LMS.IdentityService.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/profile");

        // GET /api/identity/profile/me
        group.MapGet("/me", async (
            ITenantContext ctx,
            IUserProfileRepository repo,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var profile = await repo.FindByKeycloakIdAsync(ctx.TenantId, ctx.UserId.ToString(), ct);
            return profile is null
                ? ResultExtensions.ProblemNotFound("PROFILE_NOT_FOUND", "Profile not found")
                : Results.Ok(profile.ToDto());
        });

        // PUT /api/identity/profile/me
        group.MapPut("/me", async (
            UpdateProfileRequest req,
            ITenantContext ctx,
            IUserProfileRepository repo,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            if (string.IsNullOrWhiteSpace(req.DisplayName) || req.DisplayName.Length > 120)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["DisplayName"] = ["DisplayName must be between 1 and 120 characters"]
                });

            if (req.Timezone is not null && !IsValidIanaTimezone(req.Timezone))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Timezone"] = ["Invalid IANA timezone identifier"] });
            if (req.Language is not null && !IsValidLanguage(req.Language))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Language"] = ["Unsupported language code"] });
            if (!IsValidHttpsUrl(req.AvatarUrl))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["AvatarUrl"] = ["Must be a valid HTTPS URL"] });

            var profile = await repo.FindByKeycloakIdAsync(ctx.TenantId, ctx.UserId.ToString(), ct);
            if (profile is null)
                return ResultExtensions.ProblemNotFound("PROFILE_NOT_FOUND", "Profile not found");

            profile.DisplayName = req.DisplayName;
            if (req.AvatarUrl is not null) profile.AvatarUrl = req.AvatarUrl;
            if (req.Bio is not null) profile.Bio = req.Bio;
            if (req.Timezone is not null) profile.Timezone = req.Timezone;
            if (req.Language is not null) profile.Language = req.Language;
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToDto());
        });

        // POST /api/identity/profile (upsert)
        group.MapPost("/", async (
            UpsertProfileRequest req,
            ITenantContext ctx,
            IUserProfileRepository profileRepo,
            ITenantConfigRepository tenantRepo,
            IUserInviteRepository inviteRepo,
            IdentityDbContext db,
            IPublishEndpoint publish,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            if (string.IsNullOrWhiteSpace(req.DisplayName) || req.DisplayName.Length > 120)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["DisplayName"] = ["DisplayName must be between 1 and 120 characters"]
                });

            if (req.Timezone is not null && !IsValidIanaTimezone(req.Timezone))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Timezone"] = ["Invalid IANA timezone identifier"] });
            if (req.Language is not null && !IsValidLanguage(req.Language))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Language"] = ["Unsupported language code"] });
            if (!IsValidHttpsUrl(req.AvatarUrl))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["AvatarUrl"] = ["Must be a valid HTTPS URL"] });

            var tenantConfig = await tenantRepo.FindAsync(ctx.TenantId, ct);
            if (tenantConfig is null)
                return ResultExtensions.ProblemConflict("TENANT_NOT_PROVISIONED", "Tenant is not provisioned");

            var existing = await profileRepo.FindByKeycloakIdAsync(ctx.TenantId, req.KeycloakId, ct);
            if (existing is not null)
            {
                // Update existing — no event published
                existing.Email = req.Email;
                existing.DisplayName = req.DisplayName;
                if (req.AvatarUrl is not null) existing.AvatarUrl = req.AvatarUrl;
                if (req.Timezone is not null) existing.Timezone = req.Timezone;
                if (req.Language is not null) existing.Language = req.Language;
                existing.UpdatedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(ct);
                return Results.Ok(existing.ToDto());
            }

            // Create new profile
            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                KeycloakId = req.KeycloakId,
                Email = req.Email,
                DisplayName = req.DisplayName,
                AvatarUrl = req.AvatarUrl,
                Timezone = req.Timezone ?? "Asia/Ho_Chi_Minh",
                Language = req.Language ?? "vi",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await profileRepo.AddAsync(profile, ct);

            // Mark pending invite accepted if found
            var pendingInvite = await inviteRepo.FindPendingByEmailAsync(ctx.TenantId, req.Email, ct);
            if (pendingInvite is not null)
            {
                pendingInvite.IsAccepted = true;
                pendingInvite.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // Publish UserRegistered via outbox
            await publish.Publish(new UserRegistered(
                EventId: Guid.NewGuid(),
                TenantId: ctx.TenantId,
                UserId: profile.Id,
                Email: profile.Email,
                DisplayName: profile.DisplayName,
                OccurredAt: DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/identity/profile/{profile.Id}", profile.ToDto());
        });

        return app;
    }
}
