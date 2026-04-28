using LMS.SharedKernel;

namespace LMS.CourseService.Domain.Entities;

public class CoursePrerequisite : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
}
