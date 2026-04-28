namespace LMS.AssessmentService.Domain.DTOs;

public record AssessmentResultDto(
    Guid AttemptId,
    float Score,
    float MaxScore,
    bool Passed,
    int TimeTakenSeconds,
    IReadOnlyList<GradedAnswerDto> Answers);

public record GradedAnswerDto(
    Guid QuestionId,
    int? SelectedOptionIndex,
    int CorrectOptionIndex,
    bool? IsCorrect,
    int PointsAwarded,
    string? Explanation);
