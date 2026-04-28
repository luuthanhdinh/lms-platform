using LMS.SharedKernel;

namespace LMS.ProgressService.Domain.Entities;

public class CourseProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public float CompletionPercent { get; set; }
    public int LessonsCompleted { get; set; }
    public int TotalRequiredLessons { get; set; }        // IsOptional=false lessons only
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Idempotency gate — true once CourseCompleted event has been published.</summary>
    public bool CourseCompletedEventPublished { get; set; }
}
