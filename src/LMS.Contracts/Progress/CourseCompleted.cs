namespace LMS.Contracts.Progress;

/// <summary>
/// Published by ProgressService on the first transition to 100% completion for a course.
/// Consumers: CertificateService (issue certificate), NotificationWorker (completion email).
/// Dedupe key: (UserId, CourseId) — only published once per enrolment;
/// no EventId is present because the natural key is sufficient for inbox deduplication.
/// CourseName and LearnerName are nullable — ProgressService sets them null in Phase 1;
/// Phase 2 enriches once a course/user snapshot read-model exists.
/// </summary>
public sealed record CourseCompleted(
    /// <summary>Surrogate key of the student who completed the course.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the course that was completed.</summary>
    Guid CourseId,

    /// <summary>Tenant that owns the student and the course. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Display name of the course; null in Phase 1 until a course read-model snapshot exists.</summary>
    string? CourseName,

    /// <summary>Display name of the learner; null in Phase 1 until a user read-model snapshot exists.</summary>
    string? LearnerName,

    /// <summary>UTC instant the course completion (100% threshold) was persisted to the database.</summary>
    DateTimeOffset OccurredAt);
