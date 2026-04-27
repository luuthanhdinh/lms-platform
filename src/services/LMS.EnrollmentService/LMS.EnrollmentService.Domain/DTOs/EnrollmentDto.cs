using LMS.EnrollmentService.Domain.Enums;

namespace LMS.EnrollmentService.Domain.DTOs;

public record EnrollmentDto(
    Guid Id, Guid TenantId, Guid UserId, Guid CourseId,
    EnrollmentStatus Status, bool IsFree,
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
    DateTimeOffset? SuspendedAt, string? SuspensionReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
