using LMS.ProgressService.Domain.DTOs;
using LMS.ProgressService.Domain.Entities;

namespace LMS.ProgressService.Api.Mapping;

internal static class ProgressMapping
{
    internal static LessonProgressDto ToDto(this LessonProgress p) => new(
        p.Id, p.UserId, p.LessonId, p.CourseId,
        p.Status,
        p.CompletedAt, p.LastAccessedAt,
        p.CreatedAt, p.UpdatedAt);

    internal static CourseProgressDto ToDto(this CourseProgress p) => new(
        p.Id, p.UserId, p.CourseId,
        p.CompletionPercent, p.LessonsCompleted, p.TotalRequiredLessons,
        p.LastAccessedAt, p.CompletedAt,
        p.CreatedAt, p.UpdatedAt);
}
