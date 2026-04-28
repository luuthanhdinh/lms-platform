namespace LMS.Contracts.Course;

/// <summary>
/// Published by CourseService when an instructor transitions a course to Published state.
/// Consumers: EnrollmentService (open enrolment), NotificationWorker (instructor confirmation email).
/// </summary>
public sealed record CoursePublished(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Tenant that owns the course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the course that was published.</summary>
    Guid CourseId,

    /// <summary>Surrogate key of the instructor who published the course.</summary>
    Guid InstructorId,

    /// <summary>Optimistic-concurrency version of the course aggregate at the time of publishing.</summary>
    int Version,

    /// <summary>UTC instant the publish was committed to the database.</summary>
    DateTimeOffset OccurredAt);
