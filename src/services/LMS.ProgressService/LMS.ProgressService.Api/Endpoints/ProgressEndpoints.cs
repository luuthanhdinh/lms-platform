using LMS.Contracts.Progress;
using LMS.ProgressService.Api.Auth;
using LMS.ProgressService.Api.Extensions;
using LMS.ProgressService.Api.Mapping;
using LMS.ProgressService.Domain.Abstractions;
using LMS.ProgressService.Domain.DTOs;
using LMS.ProgressService.Domain.Entities;
using LMS.ProgressService.Domain.Enums;
using LMS.ProgressService.Domain.Repositories;
using LMS.ProgressService.Infrastructure.Data;
using MassTransit;

namespace LMS.ProgressService.Api.Endpoints;

internal static class ProgressEndpoints
{
    internal static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress");

        // POST /api/progress/lessons/{lessonId}/complete
        group.MapPost("/lessons/{lessonId:guid}/complete", async (
            Guid lessonId,
            CompleteLessonRequest request,
            ITenantContext ctx,
            ILessonProgressRepository lessonRepo,
            ICourseProgressRepository courseRepo,
            ProgressDbContext db,
            IPublishEndpoint publish,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var now = DateTimeOffset.UtcNow;

            var lessonProgress = await lessonRepo.FindAsync(ctx.TenantId, ctx.UserId, lessonId, ct);

            bool isNewCompletion;
            if (lessonProgress is not null && lessonProgress.Status == ProgressStatus.Completed)
            {
                // Idempotent — already completed, no publish
                return Results.Ok(lessonProgress.ToDto());
            }
            else if (lessonProgress is null)
            {
                lessonProgress = new LessonProgress
                {
                    UserId = ctx.UserId,
                    LessonId = lessonId,
                    CourseId = request.CourseId,
                    TenantId = ctx.TenantId,
                    Status = ProgressStatus.Completed,
                    CompletedAt = now,
                    LastAccessedAt = now
                };
                await lessonRepo.AddAsync(lessonProgress, ct);
                isNewCompletion = true;
            }
            else
            {
                lessonProgress.Status = ProgressStatus.Completed;
                lessonProgress.CompletedAt = now;
                lessonProgress.LastAccessedAt = now;
                await lessonRepo.UpdateAsync(lessonProgress, ct);
                isNewCompletion = true;
            }

            // Upsert CourseProgress
            var courseProgress = await courseRepo.FindAsync(ctx.TenantId, ctx.UserId, request.CourseId, ct);
            if (courseProgress is null)
            {
                courseProgress = new CourseProgress
                {
                    UserId = ctx.UserId,
                    CourseId = request.CourseId,
                    TenantId = ctx.TenantId,
                    LessonsCompleted = 1,
                    TotalRequiredLessons = 1,
                    CompletionPercent = 100,
                    LastAccessedAt = now,
                    CourseCompletedEventPublished = false
                };
                await courseRepo.AddAsync(courseProgress, ct);
            }
            else if (isNewCompletion)
            {
                courseProgress.LessonsCompleted += 1;
                courseProgress.TotalRequiredLessons = Math.Max(courseProgress.TotalRequiredLessons, courseProgress.LessonsCompleted);
                courseProgress.CompletionPercent = courseProgress.LessonsCompleted * 100f / courseProgress.TotalRequiredLessons;
                courseProgress.LastAccessedAt = now;
                await courseRepo.UpdateAsync(courseProgress, ct);
            }

            // Publish LessonCompleted
            await publish.Publish(new LessonCompleted(
                UserId: ctx.UserId,
                LessonId: lessonId,
                CourseId: request.CourseId,
                TenantId: ctx.TenantId,
                OccurredAt: now), ct);

            // Publish CourseCompleted if 100% and not yet published
            if (courseProgress.CompletionPercent >= 100 && !courseProgress.CourseCompletedEventPublished)
            {
                courseProgress.CourseCompletedEventPublished = true;
                courseProgress.CompletedAt = now;
                await courseRepo.UpdateAsync(courseProgress, ct);

                await publish.Publish(new CourseCompleted(
                    UserId: ctx.UserId,
                    CourseId: request.CourseId,
                    TenantId: ctx.TenantId,
                    CourseName: null,
                    LearnerName: null,
                    OccurredAt: now), ct);
            }

            // Commit entity changes and outbox messages atomically
            await db.SaveChangesAsync(ct);

            return Results.Ok(lessonProgress.ToDto());
        });

        // Order matters: more specific routes before generic /{courseId}
        // GET /api/progress/courses/{courseId}/analytics
        group.MapGet("/courses/{courseId:guid}/analytics", async (
            Guid courseId,
            ITenantContext ctx,
            ICourseProgressRepository courseRepo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx))
                return ResultExtensions.ProblemForbidden();

            var rows = await courseRepo.ListByCourseAsync(ctx.TenantId, courseId, ct);

            var totalEnrolled = rows.Count;
            var completed = rows.Count(r => r.CompletionPercent >= 100);
            var notStarted = rows.Count(r => r.LessonsCompleted == 0);
            var inProgress = totalEnrolled - completed - notStarted;

            var summary = new CourseProgressSummaryDto(
                CourseId: courseId,
                CompletionPercent: totalEnrolled == 0 ? 0 : rows.Average(r => r.CompletionPercent),
                LastAccessedAt: rows.Max(r => r.LastAccessedAt),
                CompletedAt: rows.Max(r => r.CompletedAt));

            return Results.Ok(new
            {
                summary.CourseId,
                summary.CompletionPercent,
                summary.LastAccessedAt,
                summary.CompletedAt,
                TotalEnrolled = totalEnrolled,
                Completed = completed,
                NotStarted = notStarted,
                InProgress = inProgress
            });
        });

        // GET /api/progress/courses/{courseId}/lessons
        group.MapGet("/courses/{courseId:guid}/lessons", async (
            Guid courseId,
            ITenantContext ctx,
            ILessonProgressRepository lessonRepo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var items = await lessonRepo.ListByCourseAsync(ctx.TenantId, ctx.UserId, courseId, ct);
            return Results.Ok(items.Select(l => l.ToDto()).ToList());
        });

        // GET /api/progress/me — all course progress for the current user
        group.MapGet("/me", async (
            ITenantContext ctx,
            ICourseProgressRepository courseRepo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var progress = await courseRepo.ListByUserAsync(ctx.TenantId, ctx.UserId, ct);
            return Results.Ok(progress.Select(p => p.ToDto()).ToList());
        });

        // GET /api/progress/courses/{courseId}
        group.MapGet("/courses/{courseId:guid}", async (
            Guid courseId,
            ITenantContext ctx,
            ICourseProgressRepository courseRepo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx))
                return ResultExtensions.ProblemUnauthorized();

            var progress = await courseRepo.FindAsync(ctx.TenantId, ctx.UserId, courseId, ct);
            if (progress is null)
                return ResultExtensions.ProblemNotFound("PROGRESS_NOT_FOUND", $"No progress found for course {courseId}");

            return Results.Ok(progress.ToDto());
        });

        return app;
    }
}
