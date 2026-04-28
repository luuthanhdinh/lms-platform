namespace LMS.Contracts.Progress;

/// <summary>
/// Published by ProgressService when a student completes a lesson.
/// Consumers: CertificateService (eligibility check), NotificationWorker (progress email).
/// Dedupe key: (UserId, LessonId) — re-completing the same lesson is idempotent;
/// no EventId is present because the natural key is sufficient for inbox deduplication.
/// </summary>
public sealed record LessonCompleted(
    Guid UserId,
    Guid LessonId,
    Guid CourseId,
    Guid TenantId,
    DateTimeOffset OccurredAt);
