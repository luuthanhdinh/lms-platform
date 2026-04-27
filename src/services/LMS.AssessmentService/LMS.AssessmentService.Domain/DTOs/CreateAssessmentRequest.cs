using LMS.AssessmentService.Domain.Enums;

namespace LMS.AssessmentService.Domain.DTOs;

public record CreateAssessmentRequest(
    Guid CourseId,
    Guid? LessonId,
    string Title,
    float PassingScore,
    int? TimeLimitSeconds,
    int MaxAttempts,
    bool IsRandomised,
    int? QuestionSampleSize);
