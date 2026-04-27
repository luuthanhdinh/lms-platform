namespace LMS.AssessmentService.Domain.DTOs;

public record AttemptSummaryDto(
    Guid AttemptId,
    float Score,
    float MaxScore,
    bool Passed,
    DateTimeOffset SubmittedAt);
