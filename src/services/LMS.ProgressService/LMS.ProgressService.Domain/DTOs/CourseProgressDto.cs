namespace LMS.ProgressService.Domain.DTOs;

public record CourseProgressDto(
    Guid Id, Guid UserId, Guid CourseId,
    float CompletionPercent, int LessonsCompleted, int TotalRequiredLessons,
    DateTimeOffset? LastAccessedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
