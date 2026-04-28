namespace LMS.Contracts.Assessment;

/// <summary>
/// Published by AssessmentService when a student submits an attempt.
/// Consumers: ProgressService (lesson-quiz-pass = lesson complete),
///            NotificationWorker (result email).
/// Idempotency key for downstream consumers: (UserId, AssessmentId, OccurredAt).
/// </summary>
public sealed record AssessmentSubmitted(
    /// <summary>Surrogate key of the student who submitted the attempt.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the Assessment that was submitted.</summary>
    Guid AssessmentId,

    /// <summary>Surrogate key of the course the assessment belongs to.</summary>
    Guid CourseId,

    /// <summary>Tenant that owns the assessment. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Percentage score achieved, in the range 0–100.</summary>
    int Score,

    /// <summary>Whether the score met or exceeded the assessment's PassingScore threshold.</summary>
    bool Passed,

    /// <summary>UTC instant the attempt was persisted to the database.</summary>
    DateTimeOffset OccurredAt);
