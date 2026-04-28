using LMS.AssessmentService.Domain.DTOs;
using LMS.AssessmentService.Domain.Entities;

namespace LMS.AssessmentService.Api.Mapping;

internal static class AssessmentMapping
{
    // Student view — strips CorrectOptionIndex and Explanation
    internal static QuestionDto ToStudentDto(this Question q) =>
        new(q.Id, q.Type, q.Prompt,
            q.Options.Select(o => o.Text).ToArray(),
            null,       // CorrectOptionIndex hidden
            null,       // Explanation hidden
            q.Points, q.Order);

    // Instructor/admin view — includes CorrectOptionIndex and Explanation
    internal static QuestionDto ToInstructorDto(this Question q) =>
        new(q.Id, q.Type, q.Prompt,
            q.Options.Select(o => o.Text).ToArray(),
            q.CorrectOptionIndex,
            q.Explanation,
            q.Points, q.Order);

    internal static AssessmentDto ToDto(this Assessment a, bool instructorView = false)
    {
        var questions = a.Questions
            .OrderBy(q => q.Order)
            .Select(q => instructorView ? q.ToInstructorDto() : q.ToStudentDto())
            .ToList();

        return new AssessmentDto(
            a.Id, a.CourseId, a.LessonId, a.Title,
            a.PassingScore, a.TimeLimitSeconds, a.MaxAttempts,
            a.IsRandomised, a.QuestionSampleSize, a.IsActive,
            questions, a.CreatedAt, a.UpdatedAt);
    }

    internal static AttemptSummaryDto ToSummaryDto(this AssessmentAttempt attempt) =>
        new(attempt.Id, attempt.Score, attempt.MaxScore, attempt.Passed, attempt.SubmittedAt);
}
