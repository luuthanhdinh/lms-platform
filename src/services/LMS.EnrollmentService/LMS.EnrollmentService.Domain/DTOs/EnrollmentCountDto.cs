namespace LMS.EnrollmentService.Domain.DTOs;

public record EnrollmentCountDto(Guid CourseId, int ActiveCount, int TotalCount);
