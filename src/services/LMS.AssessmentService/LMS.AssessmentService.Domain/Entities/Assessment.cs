using LMS.SharedKernel;

namespace LMS.AssessmentService.Domain.Entities;

public class Assessment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid? LessonId { get; set; }                // null => course exam (ADR-007)
    public string Title { get; set; } = default!;
    public float PassingScore { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public bool IsRandomised { get; set; }
    public int? QuestionSampleSize { get; set; }
    public bool IsAdaptive { get; set; }               // Phase 2 IRT
    public bool IsActive { get; set; } = true;         // flipped to false on CourseArchived
    public ICollection<Question> Questions { get; set; } = [];
}
