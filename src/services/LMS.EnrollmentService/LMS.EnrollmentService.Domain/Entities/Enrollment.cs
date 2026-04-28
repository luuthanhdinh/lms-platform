using LMS.SharedKernel;
using LMS.EnrollmentService.Domain.Enums;

namespace LMS.EnrollmentService.Domain.Entities;

public class Enrollment : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public bool IsFree { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }
}
