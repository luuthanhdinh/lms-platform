namespace LMS.AssessmentService.Domain.Sessions;

// Stored in Redis (NOT EF) — key: assessment:session:{sessionId}
public sealed class AssessmentSession
{
    public Guid SessionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid[] QuestionIds { get; set; } = [];
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
