using LMS.AssessmentService.Domain.Enums;

namespace LMS.AssessmentService.Domain.DTOs;

public record CreateQuestionRequest(
    QuestionType Type,
    string Prompt,
    string[] Options,
    int CorrectOptionIndex,
    string? Explanation,
    int Points,
    int Order);
