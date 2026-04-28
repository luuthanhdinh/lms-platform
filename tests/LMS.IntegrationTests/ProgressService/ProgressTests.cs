using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LMS.Contracts.Enrollment;
using LMS.Contracts.Progress;
using LMS.IntegrationTests.ProgressService.Fixtures;
using LMS.ProgressService.Domain.DTOs;
using LMS.ProgressService.Domain.Enums;
using LMS.ProgressService.Infrastructure.Data;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LMS.IntegrationTests.ProgressService;

[Trait("Category", "Progress")]
public sealed class ProgressTests : IAsyncLifetime
{
    private readonly ProgressFactory _factory = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // ── helpers ──────────────────────────────────────────────────────────────

    private HttpClient ClientFor(Guid tenantId, Guid userId, params string[] roles)
        => _factory.CreateClientFor(tenantId, userId, roles);

    private async Task PublishUserEnrolledAsync(Guid tenantId, Guid userId, Guid courseId)
    {
        await _factory.Harness.Bus.Publish(new UserEnrolled(
            UserId: userId,
            CourseId: courseId,
            TenantId: tenantId,
            PlanType: "Free",
            OccurredAt: DateTimeOffset.UtcNow));

        Assert.True(await _factory.Harness.Consumed.Any<UserEnrolled>());
    }

    private async Task<HttpResponseMessage> PostMarkCompleteAsync(
        HttpClient client, Guid lessonId, Guid courseId)
    {
        return await client.PostAsJsonAsync(
            $"/api/progress/lessons/{lessonId}/complete",
            new CompleteLessonRequest(courseId),
            Json);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>1. UserEnrolled event seeds a CourseProgress row with LessonsCompleted=0.</summary>
    [Fact]
    public async Task UserEnrolled_SeedsCourseProgress()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var row = await db.CourseProgress
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId);

        Assert.NotNull(row);
        Assert.Equal(0, row!.LessonsCompleted);
        Assert.Equal(0f, row.CompletionPercent);
    }

    /// <summary>2. Publishing UserEnrolled twice creates only one CourseProgress row.</summary>
    [Fact]
    public async Task UserEnrolled_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await _factory.Harness.Bus.Publish(new UserEnrolled(
            UserId: userId, CourseId: courseId, TenantId: tenantId,
            PlanType: "Free", OccurredAt: DateTimeOffset.UtcNow));
        await _factory.Harness.Bus.Publish(new UserEnrolled(
            UserId: userId, CourseId: courseId, TenantId: tenantId,
            PlanType: "Free", OccurredAt: DateTimeOffset.UtcNow));

        // Wait for at least two consumptions
        await Task.Delay(500);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var count = await db.CourseProgress
            .IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId);

        Assert.Equal(1, count);
    }

    /// <summary>3. POST complete lesson → 200, LessonProgress Status=Completed, LessonCompleted published.</summary>
    [Fact]
    public async Task Post_MarkLessonComplete_Returns200AndPublishesLessonCompleted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        var response = await PostMarkCompleteAsync(client, lessonId, courseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<LessonProgressDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(ProgressStatus.Completed, dto!.Status);
        Assert.NotNull(dto.CompletedAt);

        Assert.True(await _factory.Harness.Published.Any<LessonCompleted>(
            m => m.Context.Message.UserId == userId && m.Context.Message.LessonId == lessonId));
    }

    /// <summary>4. Calling POST complete twice for the same lesson → second returns 200 without duplicate publish.</summary>
    [Fact]
    public async Task Post_MarkLessonComplete_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        var first = await PostMarkCompleteAsync(client, lessonId, courseId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostMarkCompleteAsync(client, lessonId, courseId);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // LessonCompleted should be published exactly once
        int lessonCompletedCount = 0;
        await foreach (var msg in _factory.Harness.Published
            .SelectAsync<LessonCompleted>(m => m.Context.Message.UserId == userId && m.Context.Message.LessonId == lessonId))
        {
            lessonCompletedCount++;
        }
        Assert.Equal(1, lessonCompletedCount);
    }

    /// <summary>5. When all required lessons complete, CourseCompleted is published and CompletionPercent=100.</summary>
    [Fact]
    public async Task Post_MarkLessonComplete_WhenAllLessons_PublishesCourseCompleted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        // Seed CourseProgress with TotalRequiredLessons=1 via UserEnrolled
        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        // Mark the single lesson complete
        var response = await PostMarkCompleteAsync(client, lessonId, courseId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // CourseCompleted should be published
        Assert.True(await _factory.Harness.Published.Any<CourseCompleted>(
            m => m.Context.Message.UserId == userId && m.Context.Message.CourseId == courseId));

        // Verify CompletionPercent in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var row = await db.CourseProgress
            .IgnoreQueryFilters()
            .SingleAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId);
        Assert.Equal(100f, row.CompletionPercent);
    }

    /// <summary>6. Completing the same lesson twice → CourseCompleted published exactly once.</summary>
    [Fact]
    public async Task Post_MarkLessonComplete_CourseCompleted_PublishedOnlyOnce()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        await PostMarkCompleteAsync(client, lessonId, courseId);
        await PostMarkCompleteAsync(client, lessonId, courseId);

        int courseCompletedCount = 0;
        await foreach (var msg in _factory.Harness.Published
            .SelectAsync<CourseCompleted>(m => m.Context.Message.UserId == userId && m.Context.Message.CourseId == courseId))
        {
            courseCompletedCount++;
        }
        Assert.Equal(1, courseCompletedCount);
    }

    /// <summary>7. GET course progress returns 200 with correct fields after UserEnrolled.</summary>
    [Fact]
    public async Task Get_CourseProgress_Returns200()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        var response = await client.GetAsync($"/api/progress/courses/{courseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CourseProgressDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(courseId, dto!.CourseId);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(0, dto.LessonsCompleted);
        Assert.Equal(0f, dto.CompletionPercent);
    }

    /// <summary>8. Tenant B cannot GET tenant A's course progress — returns 404.</summary>
    [Fact]
    public async Task Get_CourseProgress_CrossTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await PublishUserEnrolledAsync(tenantA, userId, courseId);

        // Tenant B same userId and courseId — different tenant, should not see the row
        var clientB = ClientFor(tenantB, userId, "student");
        var response = await clientB.GetAsync($"/api/progress/courses/{courseId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>9. GET lessons for course returns the completed lesson after POST complete.</summary>
    [Fact]
    public async Task Get_CourseProgress_Lessons_ReturnsCompletedLesson()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        await PublishUserEnrolledAsync(tenantId, userId, courseId);
        var postResp = await PostMarkCompleteAsync(client, lessonId, courseId);
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var response = await client.GetAsync($"/api/progress/courses/{courseId}/lessons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lessons = await response.Content.ReadFromJsonAsync<List<LessonProgressDto>>(Json);
        Assert.NotNull(lessons);
        Assert.Contains(lessons!, l => l.LessonId == lessonId && l.Status == ProgressStatus.Completed);
    }

    /// <summary>10. EnrollmentCancelled soft-freezes progress (updates LastAccessedAt, row still exists).</summary>
    [Fact]
    public async Task EnrollmentCancelled_SoftFreezesProgress()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await PublishUserEnrolledAsync(tenantId, userId, courseId);

        // Capture LastAccessedAt before cancel
        DateTimeOffset? beforeLastAccessed;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
            var row = await db.CourseProgress
                .IgnoreQueryFilters()
                .SingleAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId);
            beforeLastAccessed = row.LastAccessedAt;
        }

        await Task.Delay(50); // ensure timestamp difference

        await _factory.Harness.Bus.Publish(new EnrollmentCancelled(
            EventId: Guid.NewGuid(),
            TenantId: tenantId,
            EnrollmentId: Guid.NewGuid(),
            UserId: userId,
            CourseId: courseId,
            OccurredAt: DateTimeOffset.UtcNow));

        Assert.True(await _factory.Harness.Consumed.Any<EnrollmentCancelled>());

        // Row must still exist (non-destructive)
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ProgressDbContext>();
        var after = await db2.CourseProgress
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId && p.CourseId == courseId);
        Assert.NotNull(after);
        // LastAccessedAt updated
        Assert.True(after!.LastAccessedAt >= beforeLastAccessed);
    }

    /// <summary>11. A student role gets 403 on the analytics endpoint.</summary>
    [Fact]
    public async Task Get_Analytics_RequiresInstructorOrAdmin()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId, "student");

        var response = await client.GetAsync($"/api/progress/courses/{courseId}/analytics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>12. Analytics returns correct aggregate counts for completed vs in-progress rows.</summary>
    [Fact]
    public async Task Get_Analytics_ReturnsAggregateCounts()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        // Seed both users via UserEnrolled
        await _factory.Harness.Bus.Publish(new UserEnrolled(
            UserId: userA, CourseId: courseId, TenantId: tenantId,
            PlanType: "Free", OccurredAt: DateTimeOffset.UtcNow));
        await _factory.Harness.Bus.Publish(new UserEnrolled(
            UserId: userB, CourseId: courseId, TenantId: tenantId,
            PlanType: "Free", OccurredAt: DateTimeOffset.UtcNow));

        await Task.Delay(500); // wait for consumers

        // Complete lesson for userA only (userB stays InProgress/NotStarted)
        var clientA = ClientFor(tenantId, userA, "instructor");
        var postResp = await PostMarkCompleteAsync(clientA, lessonId, courseId);
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // GET analytics as instructor
        var response = await clientA.GetAsync($"/api/progress/courses/{courseId}/analytics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var completed = body.GetProperty("completed").GetInt32();
        var notStarted = body.GetProperty("notStarted").GetInt32();
        var totalEnrolled = body.GetProperty("totalEnrolled").GetInt32();

        Assert.Equal(2, totalEnrolled);
        Assert.Equal(1, completed);
        Assert.Equal(1, notStarted);
    }
}
