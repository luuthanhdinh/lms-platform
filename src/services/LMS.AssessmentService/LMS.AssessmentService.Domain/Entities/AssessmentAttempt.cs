using LMS.SharedKernel;
using LMS.AssessmentService.Domain.Enums;

namespace LMS.AssessmentService.Domain.Entities;

public class AssessmentAttempt : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public float Score { get; set; }
    public float MaxScore { get; set; }
    public bool Passed { get; set; }
    public int TimeTakenSeconds { get; set; }
    public GradingStatus GradingStatus { get; set; } = GradingStatus.AutoGraded;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ICollection<AttemptAnswer> Answers { get; set; } = [];
}
