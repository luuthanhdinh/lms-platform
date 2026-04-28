using System.Text.Json;
using LMS.CourseService.Api.Auth;
using LMS.CourseService.Api.Extensions;
using LMS.CourseService.Api.Mapping;
using LMS.CourseService.Domain.Abstractions;
using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Enums;
using LMS.CourseService.Domain.Models;
using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Data;
using LMS.Contracts.Course;
using MassTransit;

namespace LMS.CourseService.Api.Endpoints;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses");

        // GET /api/courses — public, requires X-Tenant-Id
        group.MapGet("/", async (
            ITenantContext ctx,
            ICourseRepository repo,
            string? category, string? tag, string? difficulty,
            int page = 1, int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (ctx.TenantId == Guid.Empty)
                return ResultExtensions.ProblemTenantRequired();

            DifficultyLevel? parsedDifficulty = null;
            if (difficulty is not null && Enum.TryParse<DifficultyLevel>(difficulty, ignoreCase: true, out var d))
                parsedDifficulty = d;

            var (items, total) = await repo.ListPublishedAsync(ctx.TenantId, category, tag, parsedDifficulty, page, pageSize, ct);
            return Results.Ok(new Page<CourseSummary>(items.Select(c => c.ToSummary()).ToList(), page, pageSize, total));
        });

        // POST /api/courses — instructor or admin
        group.MapPost("/", async (
            CreateCourseRequest req,
            ITenantContext ctx,
            ICourseRepository repo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var validationErrors = ValidateCourseRequest(req.Title, req.Description, req.Category, req.Language);
            if (validationErrors.Count > 0) return Results.ValidationProblem(validationErrors);

            var now = DateTimeOffset.UtcNow;
            var course = new Course
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                InstructorId = ctx.UserId,
                Title = req.Title,
                Description = req.Description,
                Category = req.Category,
                Tags = req.Tags,
                Difficulty = req.Difficulty,
                Language = req.Language,
                IsFree = req.IsFree,
                ThumbnailContentId = req.ThumbnailContentId,
                Status = CourseStatus.Draft,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await repo.AddAsync(course, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/courses/{course.Id}", course.ToDetail());
        });

        // GET /api/courses/{id} — public
        group.MapGet("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            ICourseRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();

            var course = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            return course is null
                ? ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found")
                : Results.Ok(course.ToDetail());
        });

        // PUT /api/courses/{id} — owner or admin
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCourseRequest req,
            ITenantContext ctx,
            ICourseRepository repo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var validationErrors = ValidateCourseRequest(req.Title, req.Description, req.Category, req.Language);
            if (validationErrors.Count > 0) return Results.ValidationProblem(validationErrors);

            course.Title = req.Title;
            course.Description = req.Description;
            course.Category = req.Category;
            course.Tags = req.Tags;
            course.Difficulty = req.Difficulty;
            course.Language = req.Language;
            course.IsFree = req.IsFree;
            course.ThumbnailContentId = req.ThumbnailContentId;
            course.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(course.ToDetail());
        });

        // POST /api/courses/{id}/publish — owner instructor only (ADR-005, ADR-006)
        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            ITenantContext ctx,
            ICourseRepository repo,
            ICourseSnapshotRepository snapshotRepo,
            CourseDbContext db,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var course = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            if (course.Status != CourseStatus.Draft)
                return ResultExtensions.ProblemConflict("INVALID_STATUS", "Course must be in Draft status to publish");

            var totalLessons = course.Sections.SelectMany(s => s.Lessons).Count();
            if (totalLessons == 0)
                return ResultExtensions.ProblemConflict("NO_PUBLISHED_LESSON", "Course must have at least one lesson to publish");

            // ADR-006: payment gate
            if (!course.IsFree)
                return ResultExtensions.ProblemConflict("PAYMENT_REQUIRED",
                    "Payment setup required before publishing a paid course",
                    new Dictionary<string, object?> {
                        ["type"] = "https://lms/errors/payment-required",
                        ["courseId"] = course.Id.ToString()
                    });

            // ADR-005: publish flow
            course.Version += 1;
            course.Status = CourseStatus.Published;
            course.PublishedAt = DateTimeOffset.UtcNow;
            course.UpdatedAt = DateTimeOffset.UtcNow;

            var structureJson = JsonSerializer.Serialize(new
            {
                sections = course.Sections.OrderBy(s => s.Order).Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    order = s.Order,
                    lessons = s.Lessons.OrderBy(l => l.Order).Select(l => new
                    {
                        id = l.Id,
                        title = l.Title,
                        order = l.Order,
                        contentItemId = l.ContentItemId,
                        durationSeconds = l.DurationSeconds,
                        isFreePreview = l.IsFreePreview,
                        isOptional = l.IsOptional,
                    })
                })
            });

            var now = DateTimeOffset.UtcNow;
            var snapshot = new CourseSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                CourseId = course.Id,
                Version = course.Version,
                StructureJson = structureJson,
                SnapshotAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await snapshotRepo.AddAsync(snapshot, ct);

            await bus.Publish(new CoursePublished(
                Guid.NewGuid(), ctx.TenantId, course.Id, course.InstructorId, course.Version, DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(course.ToDetail());
        });

        // POST /api/courses/{id}/unpublish — owner or admin
        group.MapPost("/{id:guid}/unpublish", async (
            Guid id,
            ITenantContext ctx,
            ICourseRepository repo,
            CourseDbContext db,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            course.Status = CourseStatus.Archived;
            course.UpdatedAt = DateTimeOffset.UtcNow;

            await bus.Publish(new CourseArchived(
                Guid.NewGuid(), ctx.TenantId, course.Id, course.InstructorId, DateTimeOffset.UtcNow), ct);

            await db.SaveChangesAsync(ct);
            return Results.Ok(course.ToDetail());
        });

        // POST /api/courses/{id}/duplicate — owner or admin
        group.MapPost("/{id:guid}/duplicate", async (
            Guid id,
            ITenantContext ctx,
            ICourseRepository repo,
            ISectionRepository sectionRepo,
            ILessonRepository lessonRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var original = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            if (original is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, original.InstructorId)) return ResultExtensions.ProblemForbidden();

            var now = DateTimeOffset.UtcNow;
            var duplicate = new Course
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                InstructorId = ctx.UserId,
                Title = $"{original.Title} (Copy)",
                Description = original.Description,
                Category = original.Category,
                Tags = original.Tags.ToArray(),
                Difficulty = original.Difficulty,
                Language = original.Language,
                IsFree = original.IsFree,
                ThumbnailContentId = original.ThumbnailContentId,
                Status = CourseStatus.Draft,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await repo.AddAsync(duplicate, ct);

            foreach (var section in original.Sections.OrderBy(s => s.Order))
            {
                var newSection = new CourseSection
                {
                    Id = Guid.NewGuid(),
                    TenantId = ctx.TenantId,
                    CourseId = duplicate.Id,
                    Title = section.Title,
                    Order = section.Order,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await sectionRepo.AddAsync(newSection, ct);

                foreach (var lesson in section.Lessons.OrderBy(l => l.Order))
                {
                    var newLesson = new CourseLesson
                    {
                        Id = Guid.NewGuid(),
                        TenantId = ctx.TenantId,
                        SectionId = newSection.Id,
                        CourseId = duplicate.Id,
                        Title = lesson.Title,
                        ContentItemId = lesson.ContentItemId,
                        DurationSeconds = lesson.DurationSeconds,
                        IsFreePreview = lesson.IsFreePreview,
                        IsOptional = lesson.IsOptional,
                        Order = lesson.Order,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    await lessonRepo.AddAsync(newLesson, ct);
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/courses/{duplicate.Id}", duplicate.ToDetail());
        });

        // GET /api/courses/{id}/syllabus — public
        group.MapGet("/{id:guid}/syllabus", async (
            Guid id,
            ITenantContext ctx,
            ICourseRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();

            var course = await repo.FindByIdWithSectionsAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");

            // Hide draft/archived syllabus from non-owners to avoid info disclosure
            if (course.Status != CourseStatus.Published &&
                !AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId))
                return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");

            var syllabus = new CourseSyllabus(
                course.Id, course.Version,
                course.Sections.OrderBy(s => s.Order).Select(s => new SyllabusSection(
                    s.Id, s.Title, s.Order,
                    s.Lessons.OrderBy(l => l.Order).Select(l => new SyllabusLesson(
                        l.Id, l.Title, l.Order, l.DurationSeconds, l.IsFreePreview)).ToList()
                )).ToList());

            return Results.Ok(syllabus);
        });

        // Section endpoints
        var sections = group.MapGroup("/{id:guid}/sections");

        sections.MapPost("/", async (
            Guid id,
            SectionRequest req,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Title"] = ["Title is required and must be <= 200 chars"] });

            var now = DateTimeOffset.UtcNow;
            var section = new CourseSection
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                CourseId = id,
                Title = req.Title,
                Order = req.Order,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await sectionRepo.AddAsync(section, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/courses/{id}/sections/{section.Id}", section.ToDto());
        });

        sections.MapPut("/{sid:guid}", async (
            Guid id,
            Guid sid,
            SectionRequest req,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var section = await sectionRepo.FindByIdAsync(ctx.TenantId, sid, ct);
            if (section is null || section.CourseId != id) return ResultExtensions.ProblemNotFound("SECTION_NOT_FOUND", "Section not found");

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Title"] = ["Title is required and must be <= 200 chars"] });

            section.Title = req.Title;
            section.Order = req.Order;
            section.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(section.ToDto());
        });

        sections.MapDelete("/{sid:guid}", async (
            Guid id,
            Guid sid,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var section = await sectionRepo.FindByIdAsync(ctx.TenantId, sid, ct);
            if (section is null || section.CourseId != id) return ResultExtensions.ProblemNotFound("SECTION_NOT_FOUND", "Section not found");

            await sectionRepo.DeleteAsync(section, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Lesson endpoints
        var lessons = sections.MapGroup("/{sid:guid}/lessons");

        lessons.MapPost("/", async (
            Guid id, Guid sid,
            CreateLessonRequest req,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            ILessonRepository lessonRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var section = await sectionRepo.FindByIdAsync(ctx.TenantId, sid, ct);
            if (section is null || section.CourseId != id) return ResultExtensions.ProblemNotFound("SECTION_NOT_FOUND", "Section not found");

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Title"] = ["Title is required and must be <= 200 chars"] });

            var now = DateTimeOffset.UtcNow;
            var lesson = new CourseLesson
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                SectionId = sid,
                CourseId = id,
                Title = req.Title,
                ContentItemId = req.ContentItemId,
                Order = req.Order,
                IsFreePreview = req.IsFreePreview,
                IsOptional = req.IsOptional,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await lessonRepo.AddAsync(lesson, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/courses/{id}/sections/{sid}/lessons/{lesson.Id}", lesson.ToDto());
        });

        lessons.MapPut("/{lid:guid}", async (
            Guid id, Guid sid, Guid lid,
            UpdateLessonRequest req,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            ILessonRepository lessonRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var section = await sectionRepo.FindByIdAsync(ctx.TenantId, sid, ct);
            if (section is null || section.CourseId != id) return ResultExtensions.ProblemNotFound("SECTION_NOT_FOUND", "Section not found");

            var lesson = await lessonRepo.FindByIdAsync(ctx.TenantId, lid, ct);
            if (lesson is null || lesson.SectionId != sid) return ResultExtensions.ProblemNotFound("LESSON_NOT_FOUND", "Lesson not found");

            if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Title"] = ["Title is required and must be <= 200 chars"] });

            lesson.Title = req.Title;
            lesson.ContentItemId = req.ContentItemId;
            lesson.Order = req.Order;
            lesson.IsFreePreview = req.IsFreePreview;
            lesson.IsOptional = req.IsOptional;
            lesson.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(lesson.ToDto());
        });

        lessons.MapDelete("/{lid:guid}", async (
            Guid id, Guid sid, Guid lid,
            ITenantContext ctx,
            ICourseRepository courseRepo,
            ISectionRepository sectionRepo,
            ILessonRepository lessonRepo,
            CourseDbContext db,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var course = await courseRepo.FindByIdAsync(ctx.TenantId, id, ct);
            if (course is null) return ResultExtensions.ProblemNotFound("COURSE_NOT_FOUND", "Course not found");
            if (!AuthorizationHelpers.IsOwnerOrAdmin(ctx, course.InstructorId)) return ResultExtensions.ProblemForbidden();

            var section = await sectionRepo.FindByIdAsync(ctx.TenantId, sid, ct);
            if (section is null || section.CourseId != id) return ResultExtensions.ProblemNotFound("SECTION_NOT_FOUND", "Section not found");

            var lesson = await lessonRepo.FindByIdAsync(ctx.TenantId, lid, ct);
            if (lesson is null || lesson.SectionId != sid) return ResultExtensions.ProblemNotFound("LESSON_NOT_FOUND", "Lesson not found");

            await lessonRepo.DeleteAsync(lesson, ct);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateCourseRequest(
        string title, string description, string category, string language)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            errors["Title"] = ["Title is required and must be <= 200 chars"];
        if (string.IsNullOrWhiteSpace(description) || description.Length > 4000)
            errors["Description"] = ["Description is required and must be <= 4000 chars"];
        if (string.IsNullOrWhiteSpace(category) || category.Length > 80)
            errors["Category"] = ["Category is required and must be <= 80 chars"];
        if (string.IsNullOrWhiteSpace(language) || language.Length > 10)
            errors["Language"] = ["Language is required"];
        return errors;
    }
}
