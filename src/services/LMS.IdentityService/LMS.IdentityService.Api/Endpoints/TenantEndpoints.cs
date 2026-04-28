using LMS.IdentityService.Api.Auth;
using LMS.IdentityService.Api.Extensions;
using LMS.IdentityService.Api.Models;
using static LMS.IdentityService.Api.Extensions.ValidationHelpers;
using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Data;

namespace LMS.IdentityService.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/tenant");

        // GET /api/identity/tenant — any authenticated
        group.MapGet("/", async (
            ITenantContext ctx,
            ITenantConfigRepository repo,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var config = await repo.FindAsync(ctx.TenantId, ct);
            return config is null
                ? ResultExtensions.ProblemNotFound("TENANT_NOT_FOUND", "Tenant configuration not found")
                : Results.Ok(config.ToDto());
        });

        // PUT /api/identity/tenant — admin/org-admin
        group.MapPut("/", async (
            UpdateTenantRequest req,
            ITenantContext ctx,
            ITenantConfigRepository repo,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsAdminOrOrgAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = ["Name is required"]
                });

            if (!IsValidHttpsUrl(req.LogoUrl))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["LogoUrl"] = ["Must be a valid HTTPS URL"] });

            if (req.Timezone is not null && !IsValidIanaTimezone(req.Timezone))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Timezone"] = ["Invalid IANA timezone identifier"] });

            var config = await repo.FindAsync(ctx.TenantId, ct);
            if (config is null)
                return ResultExtensions.ProblemNotFound("TENANT_NOT_FOUND", "Tenant configuration not found");

            config.Name = req.Name;
            if (req.LogoUrl is not null) config.LogoUrl = req.LogoUrl;
            if (req.Timezone is not null) config.Timezone = req.Timezone;
            if (req.AllowedEmailDomains is not null) config.AllowedEmailDomains = req.AllowedEmailDomains;
            config.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(config.ToDto());
        });

        return app;
    }
}
