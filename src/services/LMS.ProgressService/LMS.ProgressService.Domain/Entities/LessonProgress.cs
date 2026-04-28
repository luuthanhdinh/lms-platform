using LMS.SharedKernel;
using LMS.ProgressService.Domain.Enums;

namespace LMS.ProgressService.Domain.Entities;

public class LessonProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public Guid CourseId { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; }
}
