using LMS.SharedKernel;

namespace LMS.CourseService.Domain.Entities;

public class CourseSection : TenantEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int Order { get; set; }
    public ICollection<CourseLesson> Lessons { get; set; } = [];
}
