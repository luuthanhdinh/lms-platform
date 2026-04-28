namespace LMS.AssessmentService.Domain.DTOs;

public record AssessmentAnalyticsDto(
    Guid AssessmentId,
    int AttemptCount,
    int PassCount,
    float AvgScore,
    float AvgTimeSeconds,
    IReadOnlyList<QuestionStatDto> QuestionStats);

public record QuestionStatDto(
    Guid QuestionId,
    int CorrectCount,
    int IncorrectCount,
    float CorrectRate);
