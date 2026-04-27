using LMS.SharedKernel;
using LMS.AssessmentService.Domain.Enums;

namespace LMS.AssessmentService.Domain.Entities;

public class Question : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = default!;
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = default!;
    public QuestionOption[] Options { get; set; } = [];   // stored as jsonb
    public int CorrectOptionIndex { get; set; }
    public string? Explanation { get; set; }
    public int Points { get; set; } = 1;
    public float DifficultyRating { get; set; } = 3f;
    public int Order { get; set; }
}
