using LMS.Contracts.Enrollment;
using LMS.EnrollmentService.Api.Auth;
using LMS.EnrollmentService.Api.Extensions;
using LMS.EnrollmentService.Api.Mapping;
using LMS.EnrollmentService.Domain.Abstractions;
using LMS.EnrollmentService.Domain.DTOs;
using LMS.EnrollmentService.Domain.Entities;
using LMS.EnrollmentService.Domain.Enums;
using LMS.EnrollmentService.Domain.Repositories;
using LMS.EnrollmentService.Infrastructure.Data;
using MassTransit;

namespace LMS.EnrollmentService.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enrollments");

        // Course-scoped routes must be registered BEFORE {id:guid} to avoid ambiguity.

        // GET /api/enrollments/courses/{courseId:guid}
        group.MapGet("/courses/{courseId:guid}", async (
            Guid courseId,
            ITenantContext ctx,
            IEnrollmentRepository repo,
            string? status,
            int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            EnrollmentStatus? parsedStatus = null;
            if (status is not null && Enum.TryParse<EnrollmentStatus>(status, ignoreCase: true, out var s))
                parsedStatus = s;

            var (items, total) = await repo.ListByCourseAsync(ctx.TenantId, courseId, parsedStatus, page, pageSize, ct);
            return Results.Ok(new Page<EnrollmentDto>(items.Select(e => e.ToDto()).ToList(), page, pageSize, total));
        });

        // GET /api/enrollments/courses/{courseId:guid}/count
        group.MapGet("/courses/{courseId:guid}/count", async (
            Guid courseId,
            ITenantContext ctx,
            IEnrollmentRepository repo,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var (activeCount, totalCount) = await repo.CountByCourseAsync(ctx.TenantId, courseId, ct);
            return Results.Ok(new EnrollmentCountDto(courseId, activeCount, totalCount));
        });

        // POST /api/enrollments
        group.MapPost("/", async (
            EnrollRequest request,
            ITenantContext ctx,
            IEnrollmentRepository repo,
            EnrollmentDbContext db,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.CanEnroll(ctx)) return ResultExtensions.ProblemForbidden();

            if (!request.IsFree)
                return ResultExtensions.ProblemConflict("PAYMENT_REQUIRED", "Payment is required to enroll in a paid course");

            var existing = await repo.FindActiveAsync(ctx.TenantId, ctx.UserId, request.CourseId, ct);
            if (existing is not null)
                return ResultExtensions.ProblemConflict("ALREADY_ENROLLED", "User is already actively enrolled in this course");

            var enrollment = new Enrollment
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                UserId = ctx.UserId,
                CourseId = request.CourseId,
                Status = EnrollmentStatus.Active,
                IsFree = request.IsFree,
                EnrolledAt = DateTimeOffset.UtcNow,
            };

            await repo.AddAsync(enrollment, ct);

            // Publish before SaveChanges so the outbox captures both in the same transaction
            await bus.Publish(new UserEnrolled(
                ctx.UserId, request.CourseId, ctx.TenantId,
                request.IsFree ? "free" : "paid",
                DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/enrollments/{enrollment.Id}", enrollment.ToDto());
        });

        // GET /api/enrollments/me — current user's enrollments
        group.MapGet("/me", async (
            ITenantContext ctx,
            IEnrollmentRepository repo,
            string? status,
            int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            EnrollmentStatus? parsedStatus = null;
            if (status is not null && Enum.TryParse<EnrollmentStatus>(status, ignoreCase: true, out var s))
                parsedStatus = s;

            var result = await repo.ListByUserAsync(ctx.TenantId, ctx.UserId, parsedStatus, page, pageSize, ct);
            return Results.Ok(new Page<EnrollmentDto>(result.Items.Select(e => e.ToDto()).ToList(), page, pageSize, result.Total));
        });

        // GET /api/enrollments
        group.MapGet("/", async (
            ITenantContext ctx,
            IEnrollmentRepository repo,
            string? status,
            int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            EnrollmentStatus? parsedStatus = null;
            if (status is not null && Enum.TryParse<EnrollmentStatus>(status, ignoreCase: true, out var s))
                parsedStatus = s;

            (IReadOnlyList<Enrollment> items, int total) result;
            if (AuthorizationHelpers.IsAdmin(ctx))
                result = await repo.ListAllAsync(ctx.TenantId, parsedStatus, page, pageSize, ct);
            else
                result = await repo.ListByUserAsync(ctx.TenantId, ctx.UserId, parsedStatus, page, pageSize, ct);

            return Results.Ok(new Page<EnrollmentDto>(result.items.Select(e => e.ToDto()).ToList(), page, pageSize, result.total));
        });

        // GET /api/enrollments/{id:guid}
        group.MapGet("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            IEnrollmentRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var enrollment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (enrollment is null)
                return ResultExtensions.ProblemNotFound("ENROLLMENT_NOT_FOUND", "Enrollment not found");

            if (enrollment.UserId != ctx.UserId && !AuthorizationHelpers.IsAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            return Results.Ok(enrollment.ToDto());
        });

        // DELETE /api/enrollments/{id:guid}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            IEnrollmentRepository repo,
            EnrollmentDbContext db,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var enrollment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (enrollment is null)
                return ResultExtensions.ProblemNotFound("ENROLLMENT_NOT_FOUND", "Enrollment not found");

            if (enrollment.UserId != ctx.UserId && !AuthorizationHelpers.IsAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            if (enrollment.Status != EnrollmentStatus.Active)
                return ResultExtensions.ProblemConflict("NOT_ACTIVE", "Only active enrollments can be cancelled");

            enrollment.Status = EnrollmentStatus.Cancelled;
            enrollment.CancelledAt = DateTimeOffset.UtcNow;

            await repo.UpdateAsync(enrollment, ct);

            // Publish before SaveChanges so the outbox captures both in the same transaction
            await bus.Publish(new EnrollmentCancelled(
                Guid.NewGuid(), ctx.TenantId, enrollment.Id, enrollment.UserId, enrollment.CourseId,
                DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        return app;
    }
}
