using LMS.SharedKernel;

namespace LMS.AssessmentService.Domain.Entities;

public class AttemptAnswer : TenantEntity
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public int? SelectedOptionIndex { get; set; }
    public string? TextAnswer { get; set; }            // Phase 2 essays
    public bool? IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
}
