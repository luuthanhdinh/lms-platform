using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LMS.Contracts.Course;
using LMS.Contracts.Enrollment;
using LMS.EnrollmentService.Domain.DTOs;
using LMS.EnrollmentService.Domain.Enums;
using LMS.IntegrationTests.EnrollmentService.Fixtures;
using MassTransit.Testing;
using Xunit;

namespace LMS.IntegrationTests.EnrollmentService;

[Trait("Category", "Enrollment")]
public sealed class EnrollmentTests : IAsyncLifetime
{
    private readonly EnrollmentFactory _factory = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // ── helpers ─────────────────────────────────────────────────────────────

    private HttpClient ClientFor(Guid tenantId, Guid userId, string roles = "student")
        => _factory.CreateClientFor(tenantId, userId, roles);

    private static EnrollRequest FreeEnrollRequest(Guid? courseId = null)
        => new(courseId ?? Guid.NewGuid(), IsFree: true);

    private static EnrollRequest PaidEnrollRequest(Guid? courseId = null)
        => new(courseId ?? Guid.NewGuid(), IsFree: false);

    private async Task<EnrollmentDto> PostEnrollAsync(HttpClient client, EnrollRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/enrollments", request, Json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(Json);
        return dto!;
    }

    // ── tests ────────────────────────────────────────────────────────────────

    /// <summary>1. POST with IsFree=true → 201, row in DB, UserEnrolled published.</summary>
    [Fact]
    public async Task Post_WithIsFreeTrue_Returns201AndPublishesUserEnrolled()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);

        var response = await client.PostAsJsonAsync("/api/enrollments", FreeEnrollRequest(courseId), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(tenantId, dto!.TenantId);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(courseId, dto.CourseId);
        Assert.Equal(EnrollmentStatus.Active, dto.Status);

        // Assert message was published to the test harness
        Assert.True(await _factory.Harness.Published.Any<UserEnrolled>(
            m => m.Context.Message.UserId == userId && m.Context.Message.CourseId == courseId));
    }

    /// <summary>2. POST with IsFree=false → 409 PAYMENT_REQUIRED.</summary>
    [Fact]
    public async Task Post_WithIsFreeFalse_Returns409PaymentRequired()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);

        var response = await client.PostAsJsonAsync("/api/enrollments", PaidEnrollRequest(), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PAYMENT_REQUIRED", body);
    }

    /// <summary>3. Enroll twice → second call returns 409 ALREADY_ENROLLED.</summary>
    [Fact]
    public async Task Post_DuplicateActiveEnrollment_Returns409AlreadyEnrolled()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);
        var request = FreeEnrollRequest(courseId);

        var first = await client.PostAsJsonAsync("/api/enrollments", request, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/enrollments", request, Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("ALREADY_ENROLLED", body);
    }

    /// <summary>4. Enroll → cancel → enroll again → 201 (re-enrolment allowed).</summary>
    [Fact]
    public async Task Post_AfterCancel_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);
        var request = FreeEnrollRequest(courseId);

        var enroll = await client.PostAsJsonAsync("/api/enrollments", request, Json);
        Assert.Equal(HttpStatusCode.Created, enroll.StatusCode);
        var dto = await enroll.Content.ReadFromJsonAsync<EnrollmentDto>(Json);

        var cancel = await client.DeleteAsync($"/api/enrollments/{dto!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var reenroll = await client.PostAsJsonAsync("/api/enrollments", request, Json);
        Assert.Equal(HttpStatusCode.Created, reenroll.StatusCode);
    }

    /// <summary>5. DELETE own enrollment → 204, status=Cancelled, EnrollmentCancelled published.</summary>
    [Fact]
    public async Task Delete_ByOwner_Returns204AndPublishesEnrollmentCancelled()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);
        var dto = await PostEnrollAsync(client, FreeEnrollRequest());

        var response = await client.DeleteAsync($"/api/enrollments/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify status in DB via GET
        var get = await client.GetAsync($"/api/enrollments/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var updated = await get.Content.ReadFromJsonAsync<EnrollmentDto>(Json);
        Assert.Equal(EnrollmentStatus.Cancelled, updated!.Status);
        Assert.NotNull(updated.CancelledAt);

        // Verify event published
        Assert.True(await _factory.Harness.Published.Any<EnrollmentCancelled>(
            m => m.Context.Message.EnrollmentId == dto.Id));
    }

    /// <summary>6. Cancel an already-cancelled enrollment → 409 NOT_ACTIVE.</summary>
    [Fact]
    public async Task Delete_NonActive_Returns409NotActive()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);
        var dto = await PostEnrollAsync(client, FreeEnrollRequest());

        await client.DeleteAsync($"/api/enrollments/{dto.Id}");
        var second = await client.DeleteAsync($"/api/enrollments/{dto.Id}");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("NOT_ACTIVE", body);
    }

    /// <summary>7. GET list returns only the caller's own enrollments, not another user's.</summary>
    [Fact]
    public async Task Get_List_ReturnsOnlyCallerEnrollments()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(tenantId, userA);
        var clientB = ClientFor(tenantId, userB);

        await PostEnrollAsync(clientA, FreeEnrollRequest());
        await PostEnrollAsync(clientB, FreeEnrollRequest());

        var response = await clientA.GetAsync("/api/enrollments");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<Page<EnrollmentDto>>(Json);

        Assert.NotNull(page);
        Assert.All(page!.Items, e => Assert.Equal(userA, e.UserId));
    }

    /// <summary>8. Tenant B cannot GET tenant A's enrollment — returns 404.</summary>
    [Fact]
    public async Task Get_ById_CrossTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var clientA = ClientFor(tenantA, userA);
        var dto = await PostEnrollAsync(clientA, FreeEnrollRequest());

        var clientB = ClientFor(tenantB, userB);
        var response = await clientB.GetAsync($"/api/enrollments/{dto.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>9. Count endpoint: 2 enrollments, 1 cancelled → activeCount=1, totalCount=2.</summary>
    [Fact]
    public async Task Get_CourseCount_ReturnsActiveAndTotal()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        // Enroll userA — will cancel
        var clientA = ClientFor(tenantId, userA, "instructor");
        var dtoA = await PostEnrollAsync(clientA, FreeEnrollRequest(courseId));
        await clientA.DeleteAsync($"/api/enrollments/{dtoA.Id}");

        // Enroll userB — stays active
        var clientB = ClientFor(tenantId, userB, "instructor");
        await PostEnrollAsync(clientB, FreeEnrollRequest(courseId));

        // GET count as instructor
        var response = await clientA.GetAsync($"/api/enrollments/courses/{courseId}/count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var count = await response.Content.ReadFromJsonAsync<EnrollmentCountDto>(Json);

        Assert.NotNull(count);
        Assert.Equal(1, count!.ActiveCount);
        Assert.Equal(2, count.TotalCount);
    }

    /// <summary>10. Publishing CourseArchived suspends all active enrollments for that course.</summary>
    [Fact]
    public async Task CourseArchivedConsumer_SuspendsActiveEnrollments()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = ClientFor(tenantId, userId);
        var dto = await PostEnrollAsync(client, FreeEnrollRequest(courseId));

        await _factory.Harness.Bus.Publish(new CourseArchived(
            EventId: Guid.NewGuid(),
            TenantId: tenantId,
            CourseId: courseId,
            InstructorId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow));

        // Wait for the consumer to process the message
        Assert.True(await _factory.Harness.Consumed.Any<CourseArchived>());

        // Verify enrollment is now Suspended with reason "course-archived"
        var get = await client.GetAsync($"/api/enrollments/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var updated = await get.Content.ReadFromJsonAsync<EnrollmentDto>(Json);
        Assert.Equal(EnrollmentStatus.Suspended, updated!.Status);
        Assert.Equal("course-archived", updated.SuspensionReason);
        Assert.NotNull(updated.SuspendedAt);
    }
}
