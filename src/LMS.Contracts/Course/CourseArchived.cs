namespace LMS.Contracts.Course;

/// <summary>
/// Published by CourseService when a course is moved to Archived state.
/// Consumers: EnrollmentService (block new enrolments), NotificationWorker (enrolled-learner notice).
/// </summary>
public sealed record CourseArchived(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Tenant that owns the course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the course that was archived.</summary>
    Guid CourseId,

    /// <summary>Surrogate key of the instructor who archived the course.</summary>
    Guid InstructorId,

    /// <summary>UTC instant the archival was committed to the database.</summary>
    DateTimeOffset OccurredAt);
