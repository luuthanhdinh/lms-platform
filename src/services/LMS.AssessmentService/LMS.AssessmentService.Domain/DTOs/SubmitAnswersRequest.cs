namespace LMS.AssessmentService.Domain.DTOs;

public record SubmitAnswersRequest(
    IReadOnlyList<SubmittedAnswer> Answers);

public record SubmittedAnswer(
    Guid QuestionId,
    int? SelectedOptionIndex,
    string? TextAnswer);
