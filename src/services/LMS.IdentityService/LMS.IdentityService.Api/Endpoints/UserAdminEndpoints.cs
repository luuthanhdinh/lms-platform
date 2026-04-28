using LMS.Contracts.Identity;
using LMS.IdentityService.Api.Auth;
using LMS.IdentityService.Api.Extensions;
using LMS.IdentityService.Api.Models;
using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Domain.Entities;
using LMS.IdentityService.Domain.Enums;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;
using MassTransit;

namespace LMS.IdentityService.Api.Endpoints;

public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/users");

        // GET /api/identity/users
        group.MapGet("/", async (
            ITenantContext ctx,
            IUserProfileRepository repo,
            string? role,
            bool? active,
            string? search,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsAdminOrOrgAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            pageSize = Math.Min(pageSize, 100);
            var (items, total) = await repo.ListAsync(ctx.TenantId, role, active, search, page, pageSize, ct);
            var dtos = items.Select(p => p.ToSummaryDto()).ToList();
            return Results.Ok(new PagedResult<UserSummaryDto>(dtos, page, pageSize, total));
        });

        // POST /api/identity/users/invite
        group.MapPost("/invite", async (
            InviteUserRequest req,
            ITenantContext ctx,
            ITenantConfigRepository tenantRepo,
            IUserInviteRepository inviteRepo,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsAdminOrOrgAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Email"] = ["Email is required"]
                });

            if (!UserAdminHelpers.IsValidEmail(req.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Email"] = ["Invalid email format"] });

            var tenantConfig = await tenantRepo.FindAsync(ctx.TenantId, ct);
            if (tenantConfig is not null && tenantConfig.AllowedEmailDomains.Length > 0)
            {
                var domain = req.Email.Contains('@') ? req.Email.Split('@')[1] : string.Empty;
                if (!tenantConfig.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["Email"] = [$"Email domain is not allowed. Allowed domains: {string.Join(", ", tenantConfig.AllowedEmailDomains)}"]
                    });
            }

            if (!Enum.TryParse<UserRole>(req.Role.Replace("-", ""), ignoreCase: true, out var parsedRole))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Role"] = [$"Invalid role: {req.Role}"]
                });

            var hasPending = await inviteRepo.HasPendingInviteAsync(ctx.TenantId, req.Email, ct);
            if (hasPending)
                return ResultExtensions.ProblemConflict("INVITE_DUPLICATE", "A pending invite already exists for this email");

            var invite = new UserInvite
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                Email = req.Email,
                Role = parsedRole,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsAccepted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await inviteRepo.AddAsync(invite, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/identity/users/invite/{invite.Id}", invite.ToDto());
        });

        // PATCH /api/identity/users/{id}/role
        group.MapPatch("/{id:guid}/role", async (
            Guid id,
            RoleUpdateRequest req,
            ITenantContext ctx,
            IUserProfileRepository repo,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsAdminOrOrgAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            if (!Enum.TryParse<UserRole>(req.Role.Replace("-", ""), ignoreCase: true, out var parsedRole))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = [$"Invalid role: {req.Role}"]
                });

            var profile = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (profile is null)
                return ResultExtensions.ProblemNotFound("USER_NOT_FOUND", "User not found");

            profile.Role = parsedRole;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(profile.ToDto());
        });

        // PATCH /api/identity/users/{id}/deactivate
        group.MapPatch("/{id:guid}/deactivate", async (
            Guid id,
            ITenantContext ctx,
            IUserProfileRepository repo,
            IdentityDbContext db,
            IPublishEndpoint publish,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsAdminOrOrgAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            var profile = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (profile is null)
                return ResultExtensions.ProblemNotFound("USER_NOT_FOUND", "User not found");

            if (!profile.IsActive)
                return ResultExtensions.ProblemConflict("ALREADY_DEACTIVATED", "User is already deactivated");

            profile.IsActive = false;
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            await publish.Publish(new UserDeactivated(
                EventId: Guid.NewGuid(),
                TenantId: ctx.TenantId,
                UserId: profile.Id,
                DeactivatedBy: ctx.UserId,
                OccurredAt: DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}

// Local request model for role update
public record RoleUpdateRequest(string Role);

file static class UserAdminHelpers
{
    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
