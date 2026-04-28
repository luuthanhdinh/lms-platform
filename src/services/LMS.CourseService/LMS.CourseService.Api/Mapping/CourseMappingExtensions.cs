using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Models;

namespace LMS.CourseService.Api.Mapping;

public static class CourseMappingExtensions
{
    public static CourseSummary ToSummary(this Course c) => new(
        c.Id, c.Title, c.Category, c.Tags, c.Difficulty, c.Language,
        c.Status, c.IsFree, c.Version, c.EnrollmentCount, c.InstructorId, c.PublishedAt);

    public static CourseDetail ToDetail(this Course c) => new(
        c.Id, c.Title, c.Category, c.Tags, c.Difficulty, c.Language,
        c.Status, c.IsFree, c.Version, c.EnrollmentCount, c.InstructorId, c.PublishedAt,
        c.Description, c.ThumbnailContentId,
        c.Sections.OrderBy(s => s.Order).Select(s => s.ToDto()).ToList(),
        c.Prerequisites.Select(p => p.PrerequisiteCourseId).ToList());

    public static SectionDto ToDto(this CourseSection s) => new(
        s.Id, s.Title, s.Order,
        s.Lessons.OrderBy(l => l.Order).Select(l => l.ToDto()).ToList());

    public static LessonDto ToDto(this CourseLesson l) => new(
        l.Id, l.Title, l.ContentItemId, l.DurationSeconds, l.Order, l.IsFreePreview, l.IsOptional);
}
