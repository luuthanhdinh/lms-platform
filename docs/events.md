# Event Contracts — `LMS.Contracts`

All events are C# records in `LMS.Contracts/Events/`.
Every event includes `TenantId` and `OccurredAt`.
Publishers and consumers are listed for each event.

---

## How to publish

```csharp
// Inject IPublishEndpoint in any service
public class SomeEndpoint(IPublishEndpoint bus)
{
    public async Task Handle(...)
    {
        await bus.Publish(new LessonCompleted(
            UserId: userId,
            LessonId: lessonId,
            CourseId: courseId,
            TenantId: tenantId,
            WatchPercent: 100f,
            OccurredAt: DateTimeOffset.UtcNow));
    }
}
```

## How to consume

```csharp
public class LessonCompletedConsumer(AppDbContext db)
    : IConsumer<LessonCompleted>
{
    public async Task Consume(ConsumeContext<LessonCompleted> context)
    {
        var msg = context.Message;
        // handle ...
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
```

---

## All Contracts

```csharp
// ── Identity & Enrollment ──────────────────────────────────────────────
public record UserRegistered(
    Guid EventId, Guid TenantId, Guid UserId,
    string Email, string DisplayName, DateTimeOffset OccurredAt);

public record UserDeactivated(
    Guid EventId, Guid TenantId, Guid UserId,
    Guid DeactivatedBy, DateTimeOffset OccurredAt);

public record UserEnrolled(
    Guid UserId, Guid CourseId, Guid TenantId,
    string PlanType, DateTimeOffset OccurredAt);

public record EnrollmentSuspended(
    Guid UserId, Guid CourseId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);

// ── Content & Learning ────────────────────────────────────────────────
public record CoursePublished(
    Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, int Version, DateTimeOffset OccurredAt);

public record CourseArchived(
    Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, DateTimeOffset OccurredAt);

public record LessonCompleted(
    Guid UserId, Guid LessonId, Guid CourseId,
    Guid TenantId, float WatchPercent, DateTimeOffset OccurredAt);

public record CourseCompleted(
    Guid UserId, Guid CourseId, Guid TenantId,
    DateTimeOffset OccurredAt);

public record ContentProcessingCompleted(
    Guid ContentItemId, Guid TenantId,
    string HlsManifestUrl, int DurationSeconds,
    DateTimeOffset OccurredAt);

public record ContentProcessingFailed(
    Guid ContentItemId, Guid TenantId,
    string Error, DateTimeOffset OccurredAt);

public record ContentCaptioned(
    Guid ContentItemId, Guid TenantId,
    string Language, string VttUrl, DateTimeOffset OccurredAt);

// Phase 4
public record CourseLocalePublished(
    Guid CourseId, Guid TenantId, string Language,
    DateTimeOffset OccurredAt);

// ── Assessment ────────────────────────────────────────────────────────
public record AssessmentSubmitted(
    Guid UserId, Guid AssessmentId, Guid CourseId, Guid TenantId,
    float Score, bool Passed, DateTimeOffset OccurredAt);

public record EssaySubmitted(
    Guid UserId, Guid SubmissionId, Guid AssessmentId,
    Guid TenantId, DateTimeOffset OccurredAt);

public record GradeReleased(
    Guid UserId, Guid SubmissionId, Guid TenantId,
    float FinalScore, DateTimeOffset OccurredAt);

public record PlagiarismFlagged(
    Guid SubmissionId, Guid TenantId,
    float SimilarityScore, string FlagLevel,     // clear|warning|flag
    DateTimeOffset OccurredAt);

// Phase 4
public record LabCompleted(
    Guid UserId, Guid LabDefinitionId, Guid SessionId,
    Guid TenantId, DateTimeOffset OccurredAt);

// ── Gamification ─────────────────────────────────────────────────────
public record AchievementUnlocked(
    Guid UserId, Guid BadgeId, string BadgeName,
    string Rarity, int XpAwarded,
    Guid TenantId, DateTimeOffset OccurredAt);

public record LevelUp(
    Guid UserId, int NewLevel, int TotalXp,
    int XpToNextLevel, Guid TenantId, DateTimeOffset OccurredAt);

public record StreakMaintained(
    Guid UserId, int CurrentStreak,
    Guid TenantId, DateTimeOffset OccurredAt);

public record StreakBroken(
    Guid UserId, int StreakLength,
    Guid TenantId, DateTimeOffset OccurredAt);

public record TaskCompleted(
    Guid UserId, Guid TaskDefinitionId,
    Guid TenantId, DateTimeOffset OccurredAt);

public record GoalCompleted(
    Guid UserId, Guid GoalId,
    Guid TenantId, DateTimeOffset OccurredAt);

// ── Live & Social ─────────────────────────────────────────────────────
public record LiveSessionAttended(
    Guid UserId, Guid SessionId, Guid CourseId,
    int DurationMinutes, bool IsEligible,
    Guid TenantId, DateTimeOffset OccurredAt);

public record PeerReviewCompleted(
    Guid UserId, Guid SubmissionId, float AggregateScore,
    Guid TenantId, DateTimeOffset OccurredAt);

public record MentorMatchClosed(
    Guid MentorId, Guid MenteeId, Guid CourseId,
    int Rating, Guid TenantId, DateTimeOffset OccurredAt);

// ── Payments & Credentials ────────────────────────────────────────────
public record PaymentProcessed(
    Guid UserId, Guid? CourseId, Guid TenantId,
    decimal Amount, string Currency,
    string StripePaymentIntentId, DateTimeOffset OccurredAt);

public record PaymentFailed(
    Guid UserId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);

public record SubscriptionCancelled(
    Guid UserId, Guid TenantId,
    string PlanId, DateTimeOffset OccurredAt);

public record CredentialIssued(
    Guid UserId, Guid CourseId, Guid TenantId,
    Guid CredentialId, DateTimeOffset OccurredAt);

public record CredentialRevoked(
    Guid CredentialId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);

// Phase 4
public record BlockchainAnchorConfirmed(
    Guid CredentialId, Guid TenantId,
    string TxHash, string Network, DateTimeOffset OccurredAt);

public record BlockchainAnchorFailed(
    Guid CredentialId, Guid TenantId,
    string Error, DateTimeOffset OccurredAt);

// ── Engagement & AI ───────────────────────────────────────────────────
public record FrustrationDetected(
    Guid UserId, Guid ContentId, string SessionId,
    float Score, Guid TenantId, DateTimeOffset OccurredAt);

public record BoredomDetected(
    Guid UserId, Guid ContentId, string SessionId,
    float Score, Guid TenantId, DateTimeOffset OccurredAt);

public record SpacedRepetitionDue(
    Guid UserId, string TopicSlug,
    DateTimeOffset NextReviewAt, Guid TenantId, DateTimeOffset OccurredAt);

public record LearnerAtRisk(
    Guid UserId, Guid CourseId, Guid TenantId,
    float RiskScore, string[] Factors, DateTimeOffset OccurredAt);

public record GenerationJobCompleted(
    Guid JobId, Guid InstructorId, Guid TenantId,
    Guid CourseId, DateTimeOffset OccurredAt);

public record TranslationJobCompleted(
    Guid JobId, Guid CourseId, Guid TenantId,
    string Language, DateTimeOffset OccurredAt);

// ── Compliance & Platform ─────────────────────────────────────────────
public record GdprErasureRequested(
    Guid UserId, Guid RequestId, Guid TenantId,
    DateTimeOffset OccurredAt);

public record UserDataErased(
    Guid UserId, Guid RequestId, string ServiceName,
    Guid TenantId, DateTimeOffset OccurredAt);

public record TrainingOverdue(
    Guid UserId, Guid AssignmentId, Guid TenantId,
    int DaysOverdue, DateTimeOffset OccurredAt);

public record AuditEvent(
    Guid TenantId, Guid ActorId, string ActorRole,
    string Action, string ResourceType, Guid ResourceId,
    string? IpAddress, string? UserAgent,
    DateTimeOffset OccurredAt);

// Phase 4
public record LabSessionTerminated(
    Guid SessionId, Guid UserId, Guid TenantId,
    string Reason, int DurationMinutes, DateTimeOffset OccurredAt);
```

---

## Event → Service routing (Phase 1 only)

| Event | Publisher | Consumers |
|---|---|---|
| `UserRegistered` | IdentityService | NotificationWorker |
| `UserDeactivated` | IdentityService | EnrollmentService (suspend enrollments), NotificationWorker |
| `CoursePublished` | CourseService | EnrollmentService (open enrolment), NotificationWorker |
| `CourseArchived` | CourseService | EnrollmentService (suspend active enrollments), NotificationWorker |
| `UserEnrolled` | EnrollmentService | ProgressService (seed record), CourseService (increment count), NotificationWorker |
| `LessonCompleted` | ProgressService | GamificationService*, NotificationWorker |
| `CourseCompleted` | ProgressService | CertificateService, GamificationService* |
| `ContentProcessingCompleted` | ContentService | CourseService (update duration) |
| `AssessmentSubmitted` | AssessmentService | ProgressService, GamificationService* |
| `CredentialIssued` | CertificateService | NotificationWorker |

*GamificationService is Phase 2 — NotificationWorker stubs the XP award in Phase 1.
