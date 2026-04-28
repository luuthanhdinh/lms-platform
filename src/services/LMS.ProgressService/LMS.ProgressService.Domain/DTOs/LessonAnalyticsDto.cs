namespace LMS.ProgressService.Domain.DTOs;

public record LessonAnalyticsDto(
    Guid LessonId, int CompletedCount, int InProgressCount,
    float AvgWatchPercent);
