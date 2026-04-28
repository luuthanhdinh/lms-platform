namespace LMS.Contracts.Enrollment;

/// <summary>
/// Published by EnrollmentService when a learner is successfully enrolled in a course.
/// Consumers: ProgressService (seed progress record), CourseService (increment enrolment count),
/// NotificationWorker (welcome-to-course email).
/// Idempotency note: no EventId — consumers key on the (UserId, CourseId) natural unique pair.
/// </summary>
public sealed record UserEnrolled(
    /// <summary>Surrogate key of the learner who enrolled.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the course the learner enrolled in.</summary>
    Guid CourseId,

    /// <summary>Tenant that owns both the learner and the course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Billing plan under which enrolment was granted (e.g. "Free", "Pro", "Enterprise").</summary>
    string PlanType,

    /// <summary>UTC instant the enrolment record was committed to the database.</summary>
    DateTimeOffset OccurredAt);
