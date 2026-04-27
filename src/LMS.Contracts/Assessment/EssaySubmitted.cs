namespace LMS.Contracts.Assessment;

/// <summary>
/// Phase 2 placeholder — declared now so consumers can register in advance.
/// NOT published in Phase 1. Published by AssessmentService when a student
/// submits an essay-type question for instructor grading.
/// Consumers (Phase 2): GradingWorker (queue for review), NotificationWorker (instructor alert).
/// </summary>
public sealed record EssaySubmitted(
    /// <summary>Surrogate key of the student who submitted the essay.</summary>
    Guid UserId,

    /// <summary>Surrogate key of the essay submission row.</summary>
    Guid SubmissionId,

    /// <summary>Surrogate key of the Assessment the essay belongs to.</summary>
    Guid AssessmentId,

    /// <summary>Tenant that owns the assessment. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>UTC instant the essay was submitted.</summary>
    DateTimeOffset OccurredAt);
