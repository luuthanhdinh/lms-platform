using LMS.AssessmentService.Api.Auth;
using LMS.AssessmentService.Api.Extensions;
using LMS.AssessmentService.Api.Mapping;
using LMS.AssessmentService.Domain.Abstractions;
using LMS.AssessmentService.Domain.DTOs;
using LMS.AssessmentService.Domain.Entities;
using LMS.AssessmentService.Domain.Enums;
using LMS.AssessmentService.Domain.Repositories;
using LMS.AssessmentService.Domain.Sessions;
using LMS.AssessmentService.Infrastructure.Data;
using LMS.Contracts.Assessment;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LMS.AssessmentService.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assessments");

        // POST /api/assessments — create assessment (instructor or admin)
        group.MapPost("/", async (
            CreateAssessmentRequest req,
            ITenantContext ctx,
            IAssessmentRepository repo,
            AssessmentDbContext db,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = new Assessment
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                CourseId = req.CourseId,
                LessonId = req.LessonId,
                Title = req.Title,
                PassingScore = req.PassingScore,
                TimeLimitSeconds = req.TimeLimitSeconds,
                MaxAttempts = req.MaxAttempts,
                IsRandomised = req.IsRandomised,
                QuestionSampleSize = req.QuestionSampleSize,
                IsActive = true,
            };

            await repo.AddAsync(assessment, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/assessments/{assessment.Id}", assessment.ToDto(instructorView: true));
        });

        // POST /api/assessments/{id}/questions — add question (instructor or admin)
        group.MapPost("/{id:guid}/questions", async (
            Guid id,
            CreateQuestionRequest req,
            ITenantContext ctx,
            IAssessmentRepository repo,
            AssessmentDbContext db,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            if (req.CorrectOptionIndex < 0 || req.CorrectOptionIndex >= req.Options.Length)
                return ResultExtensions.ProblemConflict("INVALID_QUESTION_INDEX",
                    "CorrectOptionIndex is out of range of Options array");

            var question = new Question
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                AssessmentId = assessment.Id,
                Type = req.Type,
                Prompt = req.Prompt,
                Options = req.Options.Select(o => new QuestionOption(o)).ToArray(),
                CorrectOptionIndex = req.CorrectOptionIndex,
                Explanation = req.Explanation,
                Points = req.Points,
                Order = req.Order,
            };

            db.Questions.Add(question);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/assessments/{assessment.Id}/questions/{question.Id}",
                question.ToInstructorDto());
        });

        // PUT /api/assessments/{id}/questions/{qid} — update question
        group.MapPut("/{id:guid}/questions/{qid:guid}", async (
            Guid id,
            Guid qid,
            UpdateQuestionRequest req,
            ITenantContext ctx,
            IAssessmentRepository repo,
            AssessmentDbContext db,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            var question = assessment.Questions.FirstOrDefault(q => q.Id == qid);
            if (question is null)
                return ResultExtensions.ProblemNotFound("QUESTION_NOT_FOUND", "Question not found");

            if (req.CorrectOptionIndex < 0 || req.CorrectOptionIndex >= req.Options.Length)
                return ResultExtensions.ProblemConflict("INVALID_QUESTION_INDEX",
                    "CorrectOptionIndex is out of range of Options array");

            question.Prompt = req.Prompt;
            question.Options = req.Options.Select(o => new QuestionOption(o)).ToArray();
            question.CorrectOptionIndex = req.CorrectOptionIndex;
            question.Explanation = req.Explanation;
            question.Points = req.Points;
            question.Order = req.Order;

            await db.SaveChangesAsync(ct);

            return Results.Ok(question.ToInstructorDto());
        });

        // DELETE /api/assessments/{id}/questions/{qid}
        group.MapDelete("/{id:guid}/questions/{qid:guid}", async (
            Guid id,
            Guid qid,
            ITenantContext ctx,
            IAssessmentRepository repo,
            AssessmentDbContext db,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            var question = assessment.Questions.FirstOrDefault(q => q.Id == qid);
            if (question is null)
                return ResultExtensions.ProblemNotFound("QUESTION_NOT_FOUND", "Question not found");

            db.Questions.Remove(question);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        // GET /api/assessments/{id} — get single assessment
        group.MapGet("/{id:guid}", async (
            Guid id,
            ITenantContext ctx,
            IAssessmentRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            var instructorView = AuthorizationHelpers.IsInstructorOrAdmin(ctx);

            // Students can only see active assessments
            if (!instructorView && !assessment.IsActive)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            return Results.Ok(assessment.ToDto(instructorView));
        });

        // GET /api/assessments?courseId={courseId}&lessonId={lessonId?} — list by course
        group.MapGet("/", async (
            Guid courseId,
            Guid? lessonId,
            ITenantContext ctx,
            IAssessmentRepository repo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var instructorView = AuthorizationHelpers.IsInstructorOrAdmin(ctx);

            var (items, _) = await repo.ListByCourseAsync(ctx.TenantId, courseId, 1, 200, ct);

            var filtered = items.AsEnumerable();

            // Students only see active assessments
            if (!instructorView)
                filtered = filtered.Where(a => a.IsActive);

            // Filter by lessonId if provided
            if (lessonId.HasValue)
                filtered = filtered.Where(a => a.LessonId == lessonId.Value);

            return Results.Ok(filtered.Select(a => a.ToDto(instructorView)).ToList());
        });

        // POST /api/assessments/{id}/sessions — start session (student only)
        group.MapPost("/{id:guid}/sessions", async (
            Guid id,
            ITenantContext ctx,
            IAssessmentRepository repo,
            IAttemptRepository attemptRepo,
            IAssessmentSessionService sessionService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsStudent(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            if (!assessment.IsActive)
                return ResultExtensions.ProblemConflict("ASSESSMENT_INACTIVE",
                    "This assessment is no longer active");

            // Check max attempts
            if (assessment.MaxAttempts > 0)
            {
                var attemptCount = await attemptRepo.CountByUserAsync(ctx.TenantId, ctx.UserId, assessment.Id, ct);
                if (attemptCount >= assessment.MaxAttempts)
                    return ResultExtensions.ProblemConflict("MAX_ATTEMPTS_EXCEEDED",
                        "Maximum number of attempts has been reached");
            }

            // Determine questions to include
            var allQuestions = assessment.Questions.OrderBy(q => q.Order).ToList();
            IEnumerable<Question> selectedQuestions = allQuestions;

            if (assessment.IsRandomised)
                selectedQuestions = allQuestions.OrderBy(_ => Guid.NewGuid());

            if (assessment.QuestionSampleSize.HasValue && assessment.QuestionSampleSize.Value < allQuestions.Count)
                selectedQuestions = selectedQuestions.Take(assessment.QuestionSampleSize.Value);

            var questionList = selectedQuestions.ToList();

            // Determine TTL
            var defaultTtl = config.GetValue<int>("Assessment:DefaultSessionTtlSeconds", 3600);
            var ttlSeconds = assessment.TimeLimitSeconds.HasValue
                ? assessment.TimeLimitSeconds.Value + 60
                : defaultTtl;

            var now = DateTimeOffset.UtcNow;
            var session = new AssessmentSession
            {
                SessionId = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                UserId = ctx.UserId,
                TenantId = ctx.TenantId,
                QuestionIds = questionList.Select(q => q.Id).ToArray(),
                StartedAt = now,
                ExpiresAt = now.AddSeconds(ttlSeconds),
            };

            await sessionService.CreateAsync(session, ttlSeconds, ct);

            var dto = new SessionStartedDto(
                session.SessionId,
                session.AssessmentId,
                session.StartedAt,
                session.ExpiresAt,
                questionList.Select(q => q.ToStudentDto()).ToList());

            return Results.Created($"/api/assessments/sessions/{session.SessionId}", dto);
        });

        // POST /api/assessments/sessions/{sid}/submit — submit attempt (student only)
        group.MapPost("/sessions/{sid:guid}/submit", async (
            Guid sid,
            SubmitAnswersRequest req,
            ITenantContext ctx,
            IAssessmentRepository repo,
            IAttemptRepository attemptRepo,
            IAssessmentSessionService sessionService,
            AssessmentDbContext db,
            IPublishEndpoint bus,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsStudent(ctx)) return ResultExtensions.ProblemForbidden();

            var session = await sessionService.GetAsync(sid, ct);
            if (session is null)
                return ResultExtensions.ProblemNotFound("SESSION_NOT_FOUND",
                    "Session not found or has expired");

            // Leak-safe: verify ownership
            if (session.UserId != ctx.UserId || session.TenantId != ctx.TenantId)
                return ResultExtensions.ProblemNotFound("SESSION_NOT_FOUND",
                    "Session not found or has expired");

            var assessment = await repo.FindByIdAsync(ctx.TenantId, session.AssessmentId, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            // Build answer lookup
            var answerLookup = req.Answers.ToDictionary(a => a.QuestionId);
            var questionLookup = assessment.Questions.ToDictionary(q => q.Id);

            var answers = new List<AttemptAnswer>();
            var gradedAnswers = new List<GradedAnswerDto>();
            var hasPendingReview = false;

            foreach (var questionId in session.QuestionIds)
            {
                if (!questionLookup.TryGetValue(questionId, out var question))
                    continue;

                answerLookup.TryGetValue(questionId, out var submitted);

                bool? isCorrect = null;
                int pointsAwarded = 0;

                if (question.Type is QuestionType.Mcq or QuestionType.TrueFalse)
                {
                    isCorrect = submitted?.SelectedOptionIndex == question.CorrectOptionIndex;
                    pointsAwarded = isCorrect == true ? question.Points : 0;
                }
                else // ShortAnswer or Essay — Phase 1: PendingReview
                {
                    isCorrect = null;
                    pointsAwarded = 0;
                    hasPendingReview = true;
                }

                answers.Add(new AttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    TenantId = ctx.TenantId,
                    AttemptId = Guid.Empty, // set after attempt created
                    QuestionId = questionId,
                    SelectedOptionIndex = submitted?.SelectedOptionIndex,
                    TextAnswer = submitted?.TextAnswer,
                    IsCorrect = isCorrect,
                    PointsAwarded = pointsAwarded,
                });

                gradedAnswers.Add(new GradedAnswerDto(
                    questionId,
                    submitted?.SelectedOptionIndex,
                    question.CorrectOptionIndex,
                    isCorrect,
                    pointsAwarded,
                    question.Explanation));
            }

            // Score calculation
            float totalPoints = assessment.Questions
                .Where(q => session.QuestionIds.Contains(q.Id))
                .Sum(q => q.Points);
            float earned = answers.Sum(a => a.PointsAwarded);
            float scorePercent = totalPoints > 0 ? (earned / totalPoints) * 100f : 0f;
            bool passed = scorePercent >= assessment.PassingScore;

            var now = DateTimeOffset.UtcNow;
            var timeTaken = (int)(now - session.StartedAt).TotalSeconds;

            var attempt = new AssessmentAttempt
            {
                Id = Guid.NewGuid(),
                TenantId = ctx.TenantId,
                AssessmentId = assessment.Id,
                UserId = ctx.UserId,
                Score = earned,
                MaxScore = totalPoints,
                Passed = passed,
                TimeTakenSeconds = timeTaken,
                GradingStatus = hasPendingReview ? GradingStatus.PendingReview : GradingStatus.AutoGraded,
                StartedAt = session.StartedAt,
                SubmittedAt = now,
            };

            // Fix up AttemptId on answers
            foreach (var answer in answers)
                answer.AttemptId = attempt.Id;

            attempt.Answers = answers;

            await attemptRepo.AddAsync(attempt, ct);

            // Publish via outbox (same transaction as db.SaveChangesAsync below)
            await bus.Publish(new AssessmentSubmitted(
                UserId: ctx.UserId,
                AssessmentId: assessment.Id,
                CourseId: assessment.CourseId,
                TenantId: ctx.TenantId,
                Score: (int)Math.Round(scorePercent),
                Passed: passed,
                OccurredAt: now), ct);

            await db.SaveChangesAsync(ct);

            // Delete session best-effort
            await sessionService.DeleteAsync(sid, ct);

            var result = new AssessmentResultDto(
                attempt.Id,
                earned,
                totalPoints,
                passed,
                timeTaken,
                gradedAnswers);

            return Results.Created($"/api/assessments/attempts/{attempt.Id}", result);
        });

        // GET /api/assessments/{id}/attempts/me — student's attempts for this assessment
        group.MapGet("/{id:guid}/attempts/me", async (
            Guid id,
            ITenantContext ctx,
            IAttemptRepository attemptRepo,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();

            var attempts = await attemptRepo.ListByUserAsync(ctx.TenantId, ctx.UserId, id, ct);
            return Results.Ok(attempts.Select(a => a.ToSummaryDto()).ToList());
        });

        // GET /api/assessments/{id}/analytics — instructor/admin view
        group.MapGet("/{id:guid}/analytics", async (
            Guid id,
            ITenantContext ctx,
            IAssessmentRepository repo,
            AssessmentDbContext db,
            CancellationToken ct) =>
        {
            if (ctx.TenantId == Guid.Empty) return ResultExtensions.ProblemTenantRequired();
            if (!AuthorizationHelpers.IsAuthenticated(ctx)) return ResultExtensions.ProblemUnauthorized();
            if (!AuthorizationHelpers.IsInstructorOrAdmin(ctx)) return ResultExtensions.ProblemForbidden();

            var assessment = await repo.FindByIdAsync(ctx.TenantId, id, ct);
            if (assessment is null)
                return ResultExtensions.ProblemNotFound("ASSESSMENT_NOT_FOUND", "Assessment not found");

            var attempts = await db.AssessmentAttempts
                .Include(a => a.Answers)
                .Where(a => a.TenantId == ctx.TenantId && a.AssessmentId == id)
                .ToListAsync(ct);

            var attemptCount = attempts.Count;
            var passCount = attempts.Count(a => a.Passed);
            var avgScore = attemptCount > 0 ? attempts.Average(a => a.MaxScore > 0 ? (a.Score / a.MaxScore) * 100f : 0f) : 0f;
            var avgTime = attemptCount > 0 ? (float)attempts.Average(a => a.TimeTakenSeconds) : 0f;

            var allAnswers = attempts.SelectMany(a => a.Answers).ToList();
            var questionStats = assessment.Questions.Select(q =>
            {
                var qAnswers = allAnswers.Where(a => a.QuestionId == q.Id).ToList();
                var correct = qAnswers.Count(a => a.IsCorrect == true);
                var incorrect = qAnswers.Count(a => a.IsCorrect == false);
                var total = correct + incorrect;
                return new QuestionStatDto(q.Id, correct, incorrect,
                    total > 0 ? (float)correct / total : 0f);
            }).ToList();

            return Results.Ok(new AssessmentAnalyticsDto(
                id, attemptCount, passCount, avgScore, avgTime, questionStats));
        });

        return app;
    }
}
