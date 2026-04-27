using LMS.ProgressService.Domain.Enums;

namespace LMS.ProgressService.Domain.DTOs;

public record LessonProgressDto(
    Guid Id, Guid UserId, Guid LessonId, Guid CourseId,
    ProgressStatus Status,
    DateTimeOffset? CompletedAt, DateTimeOffset LastAccessedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
