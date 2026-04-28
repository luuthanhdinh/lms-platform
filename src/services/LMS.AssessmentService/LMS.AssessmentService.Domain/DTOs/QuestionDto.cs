using LMS.AssessmentService.Domain.Enums;

namespace LMS.AssessmentService.Domain.DTOs;

public record QuestionDto(
    Guid Id,
    QuestionType Type,
    string Prompt,
    string[] Options,
    int? CorrectOptionIndex,    // null for student view; set for instructor view
    string? Explanation,        // null for student view; set for instructor view
    int Points,
    int Order);
