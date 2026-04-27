namespace LMS.Contracts.Progress;

/// <summary>
/// Published by ProgressService on the first transition to 100% completion for a course.
/// Consumers: CertificateService (issue certificate), NotificationWorker (completion email).
/// Dedupe key: (UserId, CourseId) — only published once per enrolment;
/// no EventId is present because the natural key is sufficient for inbox deduplication.
/// </summary>
public sealed record CourseCompleted(
    /// <summary>Surrogate key of the student who completed the course.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the course that was completed.</summary>
    Guid CourseId,

    /// <summary>Tenant that owns the student and the course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>UTC instant the course completion (100% threshold) was persisted to the database.</summary>
    DateTimeOffset OccurredAt);
