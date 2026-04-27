namespace LMS.EnrollmentService.Domain.DTOs;

public record EnrollRequest(Guid CourseId, bool IsFree);
