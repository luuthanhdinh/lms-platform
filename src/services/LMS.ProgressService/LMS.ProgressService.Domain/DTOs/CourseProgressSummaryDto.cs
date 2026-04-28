namespace LMS.ProgressService.Domain.DTOs;

public record CourseProgressSummaryDto(
    Guid CourseId, float CompletionPercent,
    DateTimeOffset? LastAccessedAt, DateTimeOffset? CompletedAt);
