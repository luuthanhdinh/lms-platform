using LMS.CourseService.Domain.Enums;
using LMS.SharedKernel;

namespace LMS.CourseService.Domain.Entities;

public class Course : TenantEntity
{
    public Guid InstructorId { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid? ThumbnailContentId { get; set; }
    public string Category { get; set; } = default!;
    public string[] Tags { get; set; } = [];
    public DifficultyLevel Difficulty { get; set; }
    public string Language { get; set; } = "vi";
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public bool IsFree { get; set; } = true;
    public int Version { get; set; } = 1;
    public int EnrollmentCount { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public ICollection<CourseSection> Sections { get; set; } = [];
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = [];
}
