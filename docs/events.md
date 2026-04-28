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

## Phase 1 Contracts (implemented)

All events carry `TenantId` and `OccurredAt`. See `src/LMS.Contracts/` for full definitions.

### Identity & Enrollment

```csharp
public record UserRegistered(Guid EventId, Guid TenantId, Guid UserId,
    string Email, string DisplayName, DateTimeOffset OccurredAt);

public record UserDeactivated(Guid EventId, Guid TenantId, Guid UserId,
    Guid DeactivatedBy, DateTimeOffset OccurredAt);

public record UserEnrolled(Guid UserId, Guid CourseId, Guid TenantId,
    string PlanType, DateTimeOffset OccurredAt);

public record EnrollmentCancelled(Guid EventId, Guid TenantId, Guid EnrollmentId,
    Guid UserId, Guid CourseId, DateTimeOffset OccurredAt);
```

### Content & Learning

```csharp
public record CoursePublished(Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, int Version, DateTimeOffset OccurredAt);

public record CourseArchived(Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, DateTimeOffset OccurredAt);

public sealed record LessonCompleted(Guid UserId, Guid LessonId, Guid CourseId,
    Guid TenantId, DateTimeOffset OccurredAt);

public sealed record CourseCompleted(Guid UserId, Guid CourseId, Guid TenantId,
    string? CourseName, string? LearnerName, DateTimeOffset OccurredAt);

public record ContentUploaded(Guid EventId, Guid ContentItemId, Guid TenantId,
    Guid UploadedBy, ContentType Type, long SizeBytes, DateTimeOffset OccurredAt);

public record ContentProcessingCompleted(Guid EventId, Guid ContentItemId, Guid TenantId,
    string HlsManifestUrl, int DurationSeconds, DateTimeOffset OccurredAt, Guid UploadedBy);

public record ContentProcessingFailed(Guid EventId, Guid ContentItemId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);
```

### Assessment

```csharp
public sealed record AssessmentSubmitted(Guid UserId, Guid AssessmentId, Guid CourseId,
    Guid TenantId, int Score, bool Passed, DateTimeOffset OccurredAt);
```

### Credentials

```csharp
public sealed record CertificateIssued(Guid EventId, Guid TenantId, Guid CertificateId,
    Guid UserId, Guid CourseId, string CertificateNumber,
    Guid VerificationCode, DateTimeOffset IssuedAt, DateTimeOffset OccurredAt);
```

---

## Phase 2+ Contracts (not yet implemented)

```csharp
// Phase 2 & beyond — events defined but not wired
public record EnrollmentSuspended(...);
public record EssaySubmitted(...);
public record GradeReleased(...);
public record ContentCaptioned(...);
public record AchievementUnlocked(...);
public record LiveSessionAttended(...);
public record PaymentProcessed(...);
public record CredentialIssued(...);
// ... and others (see src/LMS.Contracts/ for full list)
```

---

## Event → Service routing (Phase 1 only)

| Event | Publisher | Consumers |
|---|---|---|
| `UserRegistered` | IdentityService | NotificationWorker (welcome email) |
| `UserDeactivated` | IdentityService | EnrollmentService (suspend enrollments), NotificationWorker (account deactivated email) |
| `CoursePublished` | CourseService | EnrollmentService (open enrolment), NotificationWorker (course-published email) |
| `CourseArchived` | CourseService | EnrollmentService (suspend active enrollments), NotificationWorker (course-archived email) |
| `UserEnrolled` | EnrollmentService | ProgressService (seed CourseProgress), CourseService (increment count), NotificationWorker (enrollment-confirmed email) |
| `EnrollmentCancelled` | EnrollmentService | ProgressService (soft-freeze progress), CourseService (decrement count), NotificationWorker (enrollment-cancelled email) |
| `LessonCompleted` | ProgressService | CertificateService (eligibility check), NotificationWorker (progress email—feature-flagged OFF); no EventId, dedupe key `(UserId, LessonId)` |
| `CourseCompleted` | ProgressService | CertificateService (issue certificate), NotificationWorker (course-completed email); gated by `CourseProgress.CourseCompletedEventPublished` (publishes once per enrollment) |
| `ContentUploaded` | ContentService.Api | ContentService.Worker (trigger processing pipeline) |
| `ContentProcessingCompleted` | ContentService.Worker | CourseService (update lesson duration), NotificationWorker (content-processing-completed email to instructor); includes `UploadedBy` |
| `ContentProcessingFailed` | ContentService.Worker | NotificationWorker (content-processing-failed alert to instructor) |
| `AssessmentSubmitted` | AssessmentService | ProgressService (lesson-quiz-pass → lesson complete), NotificationWorker (assessment-result email); no EventId, dedupe key `(UserId, AssessmentId, OccurredAt)` |
| `CertificateIssued` | CertificateService | NotificationWorker (certificate-issued email with PDF link); carries `CertificateNumber` + `VerificationCode` for verification URL |
