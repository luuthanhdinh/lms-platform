using LMS.EnrollmentService.Domain.DTOs;
using LMS.EnrollmentService.Domain.Entities;

namespace LMS.EnrollmentService.Api.Mapping;

internal static class EnrollmentMapping
{
    internal static EnrollmentDto ToDto(this Enrollment e) => new(
        e.Id, e.TenantId, e.UserId, e.CourseId,
        e.Status, e.IsFree, e.EnrolledAt,
        e.CompletedAt, e.CancelledAt, e.SuspendedAt, e.SuspensionReason,
        e.CreatedAt, e.UpdatedAt);
}
