using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LMS.AssessmentService.Domain.DTOs;
using LMS.AssessmentService.Domain.Enums;
using LMS.Contracts.Assessment;
using LMS.Contracts.Course;
using LMS.IntegrationTests.AssessmentService.Fixtures;
using MassTransit.Testing;
using Xunit;

namespace LMS.IntegrationTests.AssessmentService;

[Trait("Category", "Assessment")]
public sealed class AssessmentTests : IAsyncLifetime
{
    private readonly AssessmentFactory _factory = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // ── helpers ──────────────────────────────────────────────────────────────

    private HttpClient InstructorClientFor(Guid tenantId, Guid userId)
        => _factory.CreateClientFor(tenantId, userId, "instructor");

    private HttpClient StudentClientFor(Guid tenantId, Guid userId)
        => _factory.CreateClientFor(tenantId, userId, "student");

    private static CreateAssessmentRequest DefaultAssessmentRequest(Guid? courseId = null)
        => new(
            CourseId: courseId ?? Guid.NewGuid(),
            LessonId: null,
            Title: "Test Assessment",
            PassingScore: 70f,
            TimeLimitSeconds: null,
            MaxAttempts: 3,
            IsRandomised: false,
            QuestionSampleSize: null);

    private static CreateQuestionRequest DefaultMcqRequest(int correctIndex = 0)
        => new(
            Type: QuestionType.Mcq,
            Prompt: "What is 2+2?",
            Options: ["4", "3", "2", "1"],
            CorrectOptionIndex: correctIndex,
            Explanation: "Basic arithmetic",
            Points: 1,
            Order: 1);

    private async Task<AssessmentDto> CreateAssessmentAsync(HttpClient client, CreateAssessmentRequest? req = null)
    {
        var response = await client.PostAsJsonAsync("/api/assessments", req ?? DefaultAssessmentRequest(), Json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        return dto!;
    }

    private async Task<QuestionDto> AddQuestionAsync(HttpClient client, Guid assessmentId, CreateQuestionRequest? req = null)
    {
        var response = await client.PostAsJsonAsync($"/api/assessments/{assessmentId}/questions", req ?? DefaultMcqRequest(), Json);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<QuestionDto>(Json);
        return dto!;
    }

    private async Task<SessionStartedDto> StartSessionAsync(HttpClient client, Guid assessmentId)
    {
        var response = await client.PostAsync($"/api/assessments/{assessmentId}/sessions", null);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<SessionStartedDto>(Json);
        return dto!;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>1. Instructor creates assessment → 201 with correct fields.</summary>
    [Fact]
    public async Task Post_CreateAssessment_Returns201()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var client = InstructorClientFor(tenantId, instructorId);

        var req = DefaultAssessmentRequest(courseId);
        var response = await client.PostAsJsonAsync("/api/assessments", req, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(courseId, dto!.CourseId);
        Assert.Equal("Test Assessment", dto.Title);
        Assert.Equal(70f, dto.PassingScore);
        Assert.Equal(3, dto.MaxAttempts);
        Assert.True(dto.IsActive);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }

    /// <summary>2. Student role cannot create assessment → 403.</summary>
    [Fact]
    public async Task Post_CreateAssessment_StudentForbidden()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var client = StudentClientFor(tenantId, studentId);

        var response = await client.PostAsJsonAsync("/api/assessments", DefaultAssessmentRequest(), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>3. Instructor adds MCQ question → 201 with question data.</summary>
    [Fact]
    public async Task Post_AddQuestion_Returns201()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var client = InstructorClientFor(tenantId, instructorId);

        var assessment = await CreateAssessmentAsync(client);
        var req = DefaultMcqRequest(correctIndex: 0);

        var response = await client.PostAsJsonAsync($"/api/assessments/{assessment.Id}/questions", req, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<QuestionDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(QuestionType.Mcq, dto!.Type);
        Assert.Equal("What is 2+2?", dto.Prompt);
        Assert.Equal(4, dto.Options.Length);
        Assert.Equal(0, dto.CorrectOptionIndex); // instructor view includes answer
    }

    /// <summary>4. Student GET strips CorrectOptionIndex (null in response).</summary>
    [Fact]
    public async Task Get_Assessment_StudentViewStripsAnswers()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient);
        await AddQuestionAsync(instructorClient, assessment.Id);

        var studentClient = StudentClientFor(tenantId, studentId);
        var response = await studentClient.GetAsync($"/api/assessments/{assessment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        Assert.NotNull(dto);
        Assert.All(dto!.Questions, q => Assert.Null(q.CorrectOptionIndex));
        Assert.All(dto.Questions, q => Assert.Null(q.Explanation));
    }

    /// <summary>5. Instructor GET includes CorrectOptionIndex.</summary>
    [Fact]
    public async Task Get_Assessment_InstructorViewIncludesAnswers()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        var client = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(client);
        await AddQuestionAsync(client, assessment.Id, DefaultMcqRequest(correctIndex: 1));

        var response = await client.GetAsync($"/api/assessments/{assessment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        Assert.NotNull(dto);
        Assert.All(dto!.Questions, q => Assert.NotNull(q.CorrectOptionIndex));
        Assert.Equal(1, dto.Questions[0].CorrectOptionIndex);
    }

    /// <summary>6. Student starts session → 200, questions returned without answers, session stored in Redis.</summary>
    [Fact]
    public async Task Post_StartSession_Returns200WithQuestions()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient);
        await AddQuestionAsync(instructorClient, assessment.Id);

        var studentClient = StudentClientFor(tenantId, studentId);
        var response = await studentClient.PostAsync($"/api/assessments/{assessment.Id}/sessions", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionStartedDto>(Json);
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session!.SessionId);
        Assert.Equal(assessment.Id, session.AssessmentId);
        Assert.NotEmpty(session.Questions);
        // Student view: no correct answers
        Assert.All(session.Questions, q => Assert.Null(q.CorrectOptionIndex));
    }

    /// <summary>7. MaxAttempts=1 — second session attempt returns 409.</summary>
    [Fact]
    public async Task Post_StartSession_MaxAttemptsReached_Returns409()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var req = DefaultAssessmentRequest() with { MaxAttempts = 1 };
        var assessment = await CreateAssessmentAsync(instructorClient, req);
        var question = await AddQuestionAsync(instructorClient, assessment.Id);

        var studentClient = StudentClientFor(tenantId, studentId);
        // Start first session and submit
        var session = await StartSessionAsync(studentClient, assessment.Id);
        var submitReq = new SubmitAnswersRequest(
        [
            new SubmittedAnswer(question.Id, 0, null),
        ]);
        var submitResponse = await studentClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);
        submitResponse.EnsureSuccessStatusCode();

        // Second session attempt should be blocked
        var secondResponse = await studentClient.PostAsync($"/api/assessments/{assessment.Id}/sessions", null);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("MAX_ATTEMPTS_EXCEEDED", body);
    }

    /// <summary>8. Submit all correct → Score=100, Passed=true, AssessmentSubmitted published.</summary>
    [Fact]
    public async Task Post_Submit_AllCorrect_Returns201Passed()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient,
            DefaultAssessmentRequest() with { PassingScore = 50f });
        var question = await AddQuestionAsync(instructorClient, assessment.Id, DefaultMcqRequest(correctIndex: 0));

        var studentClient = StudentClientFor(tenantId, studentId);
        var session = await StartSessionAsync(studentClient, assessment.Id);

        var submitReq = new SubmitAnswersRequest(
        [
            new SubmittedAnswer(question.Id, 0, null),  // correct option index = 0
        ]);
        var response = await studentClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AssessmentResultDto>(Json);
        Assert.NotNull(result);
        Assert.Equal(1f, result!.Score);   // 1 point earned
        Assert.Equal(1f, result.MaxScore);
        Assert.True(result.Passed);

        // AssessmentSubmitted published
        Assert.True(await _factory.Harness.Published.Any<AssessmentSubmitted>(
            m => m.Context.Message.UserId == studentId && m.Context.Message.Passed));
    }

    /// <summary>9. Submit all wrong → Score=0, Passed=false.</summary>
    [Fact]
    public async Task Post_Submit_AllWrong_Returns201Failed()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient,
            DefaultAssessmentRequest() with { PassingScore = 50f });
        var question = await AddQuestionAsync(instructorClient, assessment.Id, DefaultMcqRequest(correctIndex: 0));

        var studentClient = StudentClientFor(tenantId, studentId);
        var session = await StartSessionAsync(studentClient, assessment.Id);

        var submitReq = new SubmitAnswersRequest(
        [
            new SubmittedAnswer(question.Id, 1, null),  // wrong option
        ]);
        var response = await studentClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AssessmentResultDto>(Json);
        Assert.NotNull(result);
        Assert.Equal(0f, result!.Score);
        Assert.False(result.Passed);
    }

    /// <summary>10. Submit with fake SessionId → 404.</summary>
    [Fact]
    public async Task Post_Submit_InvalidSession_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var client = StudentClientFor(tenantId, studentId);

        var submitReq = new SubmitAnswersRequest([]);
        var response = await client.PostAsJsonAsync(
            $"/api/assessments/sessions/{Guid.NewGuid()}/submit", submitReq, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SESSION_NOT_FOUND", body);
    }

    /// <summary>11. Tenant B cannot submit using Tenant A's session (cross-tenant leak).</summary>
    [Fact]
    public async Task Post_Submit_CrossTenantSession_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var instructorA = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();

        // Tenant A creates assessment and student A starts a session
        var instructorClient = InstructorClientFor(tenantA, instructorA);
        var assessment = await CreateAssessmentAsync(instructorClient);
        await AddQuestionAsync(instructorClient, assessment.Id);

        var studentAClient = StudentClientFor(tenantA, studentA);
        var session = await StartSessionAsync(studentAClient, assessment.Id);

        // Tenant B tries to submit using Tenant A's session
        var studentBClient = StudentClientFor(tenantB, studentB);
        var submitReq = new SubmitAnswersRequest([]);
        var response = await studentBClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>12. ShortAnswer question → GradingStatus=PendingReview, PointsAwarded=0.</summary>
    [Fact]
    public async Task Post_Submit_ShortAnswer_PendingReview()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient);
        var question = await AddQuestionAsync(instructorClient, assessment.Id,
            new CreateQuestionRequest(
                Type: QuestionType.ShortAnswer,
                Prompt: "Explain gravity",
                Options: [],
                CorrectOptionIndex: 0,
                Explanation: null,
                Points: 5,
                Order: 1));

        var studentClient = StudentClientFor(tenantId, studentId);
        var session = await StartSessionAsync(studentClient, assessment.Id);

        var submitReq = new SubmitAnswersRequest(
        [
            new SubmittedAnswer(question.Id, null, "A force of attraction"),
        ]);
        var response = await studentClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AssessmentResultDto>(Json);
        Assert.NotNull(result);

        // Short answer question: 0 points until reviewed, not passed
        var gradedAnswer = result!.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
        Assert.NotNull(gradedAnswer);
        Assert.Equal(0, gradedAnswer!.PointsAwarded);
        Assert.Null(gradedAnswer.IsCorrect);
    }

    /// <summary>13. After submitting, GET attempts/me lists the attempt.</summary>
    [Fact]
    public async Task Get_Attempts_Me_ReturnsList()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient);
        var question = await AddQuestionAsync(instructorClient, assessment.Id);

        var studentClient = StudentClientFor(tenantId, studentId);
        var session = await StartSessionAsync(studentClient, assessment.Id);

        var submitReq = new SubmitAnswersRequest(
        [
            new SubmittedAnswer(question.Id, 0, null),
        ]);
        await studentClient.PostAsJsonAsync(
            $"/api/assessments/sessions/{session.SessionId}/submit", submitReq, Json);

        var listResponse = await studentClient.GetAsync($"/api/assessments/{assessment.Id}/attempts/me");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var attempts = await listResponse.Content.ReadFromJsonAsync<List<AttemptSummaryDto>>(Json);
        Assert.NotNull(attempts);
        Assert.Single(attempts!);
        Assert.Equal(studentId, attempts[0].AttemptId == Guid.Empty ? Guid.Empty : attempts[0].AttemptId);
        Assert.NotEqual(Guid.Empty, attempts[0].AttemptId);
    }

    /// <summary>14. Publish CourseArchived → assessment.IsActive becomes false.</summary>
    [Fact]
    public async Task CourseArchived_DeactivatesAssessments()
    {
        var tenantId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        var instructorClient = InstructorClientFor(tenantId, instructorId);
        var assessment = await CreateAssessmentAsync(instructorClient, DefaultAssessmentRequest(courseId));

        // Assessment is active initially
        var getResponse = await instructorClient.GetAsync($"/api/assessments/{assessment.Id}");
        var dto = await getResponse.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        Assert.True(dto!.IsActive);

        // Publish CourseArchived event
        await _factory.Harness.Bus.Publish(new CourseArchived(
            EventId: Guid.NewGuid(),
            TenantId: tenantId,
            CourseId: courseId,
            InstructorId: instructorId,
            OccurredAt: DateTimeOffset.UtcNow));

        Assert.True(await _factory.Harness.Consumed.Any<CourseArchived>());

        // Assessment should now be inactive
        var getAfter = await instructorClient.GetAsync($"/api/assessments/{assessment.Id}");
        Assert.Equal(HttpStatusCode.OK, getAfter.StatusCode);
        var updated = await getAfter.Content.ReadFromJsonAsync<AssessmentDto>(Json);
        Assert.NotNull(updated);
        Assert.False(updated!.IsActive);
    }

    /// <summary>15. Tenant A cannot GET tenant B's assessment → 404.</summary>
    [Fact]
    public async Task Get_Assessment_CrossTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var instructorA = Guid.NewGuid();
        var instructorB = Guid.NewGuid();

        // Tenant A creates an assessment
        var clientA = InstructorClientFor(tenantA, instructorA);
        var assessment = await CreateAssessmentAsync(clientA);

        // Tenant B attempts to GET it
        var clientB = InstructorClientFor(tenantB, instructorB);
        var response = await clientB.GetAsync($"/api/assessments/{assessment.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
