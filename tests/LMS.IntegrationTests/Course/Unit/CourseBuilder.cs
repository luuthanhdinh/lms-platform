using LMS.CourseService.Domain.Entities;
using LMS.CourseService.Domain.Enums;

namespace LMS.IntegrationTests.Course.Unit;

internal static class CourseBuilder
{
    public static LMS.CourseService.Domain.Entities.Course Build(
        CourseStatus status = CourseStatus.Draft,
        bool isFree = true,
        int sectionCount = 1,
        int lessonsPerSection = 1,
        int version = 1)
    {
        var course = new LMS.CourseService.Domain.Entities.Course
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InstructorId = Guid.NewGuid(),
            Title = "Test Course",
            Description = "Test",
            Category = "Test",
            Language = "vi",
            Status = status,
            IsFree = isFree,
            Version = version,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        for (int s = 0; s < sectionCount; s++)
        {
            var section = new CourseSection
            {
                Id = Guid.NewGuid(),
                TenantId = course.TenantId,
                CourseId = course.Id,
                Title = $"Section {s}",
                Order = s,
            };

            for (int l = 0; l < lessonsPerSection; l++)
            {
                section.Lessons.Add(new CourseLesson
                {
                    Id = Guid.NewGuid(),
                    TenantId = course.TenantId,
                    SectionId = section.Id,
                    CourseId = course.Id,
                    Title = $"Lesson {l}",
                    ContentItemId = Guid.NewGuid(),
                    Order = l,
                });
            }

            course.Sections.Add(section);
        }

        return course;
    }
}
