using LMS.SharedKernel;

namespace LMS.CourseService.Domain.Entities;

public class CourseLesson : TenantEntity
{
    public Guid SectionId { get; set; }
    public CourseSection Section { get; set; } = default!;
    public Guid CourseId { get; set; }
    public string Title { get; set; } = default!;
    public Guid ContentItemId { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsFreePreview { get; set; }
    public bool IsOptional { get; set; }
    public int Order { get; set; }
}
