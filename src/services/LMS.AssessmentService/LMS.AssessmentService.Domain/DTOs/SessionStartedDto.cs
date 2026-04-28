namespace LMS.AssessmentService.Domain.DTOs;

public record SessionStartedDto(
    Guid SessionId,
    Guid AssessmentId,
    DateTimeOffset StartedAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<QuestionDto> Questions);
