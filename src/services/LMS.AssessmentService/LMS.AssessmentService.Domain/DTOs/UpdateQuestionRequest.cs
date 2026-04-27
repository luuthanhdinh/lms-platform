namespace LMS.AssessmentService.Domain.DTOs;

public record UpdateQuestionRequest(
    string Prompt,
    string[] Options,
    int CorrectOptionIndex,
    string? Explanation,
    int Points,
    int Order);
