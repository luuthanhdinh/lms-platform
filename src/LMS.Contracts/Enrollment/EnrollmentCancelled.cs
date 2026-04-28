namespace LMS.Contracts.Enrollment;

/// <summary>
/// Published by EnrollmentService when an enrolment is cancelled by the learner or an admin.
/// Consumers: ProgressService (freeze progress), CourseService (decrement enrolment count),
/// NotificationWorker (cancellation email).
/// Idempotency note: EventId is present because cancellation is NOT naturally idempotent on
/// (UserId, CourseId) — a user could re-enrol after cancelling.
/// </summary>
public sealed record EnrollmentCancelled(
    /// <summary>Unique event identifier for inbox idempotency.</summary>
    Guid EventId,

    /// <summary>Tenant that owns the enrolment. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the cancelled Enrollment row.</summary>
    Guid EnrollmentId,

    /// <summary>Surrogate key of the student whose enrolment was cancelled.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the course the enrolment was cancelled for.</summary>
    Guid CourseId,

    /// <summary>UTC instant the cancellation was persisted to the database.</summary>
    DateTimeOffset OccurredAt);
