namespace LMS.AssessmentService.Domain.Sessions;

public interface IAssessmentSessionService
{
    Task<AssessmentSession> CreateAsync(AssessmentSession session, int ttlSeconds, CancellationToken ct = default);
    Task<AssessmentSession?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteAsync(Guid sessionId, CancellationToken ct = default);
}
