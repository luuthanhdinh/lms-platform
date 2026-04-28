namespace LMS.AssessmentService.Domain.DTOs;

public record AssessmentDto(
    Guid Id,
    Guid CourseId,
    Guid? LessonId,
    string Title,
    float PassingScore,
    int? TimeLimitSeconds,
    int MaxAttempts,
    bool IsRandomised,
    int? QuestionSampleSize,
    bool IsActive,
    IReadOnlyList<QuestionDto> Questions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
