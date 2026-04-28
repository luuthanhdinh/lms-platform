# Entity Models

All Phase 1 EF Core entity definitions. Read before implementing any service.

---

## Shared Base

```csharp
// LMS.SharedKernel/Entities/TenantEntity.cs
public abstract class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

---

## IdentityService — `lms_identity`

```csharp
public class UserProfile : TenantEntity
{
    public string KeycloakId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Language { get; set; } = "vi";
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
}

public class TenantConfig : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string[] AllowedEmailDomains { get; set; } = [];
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
}

public class UserInvite : TenantEntity
{
    public string Email { get; set; } = default!;
    public UserRole Role { get; set; }
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public bool IsAccepted { get; set; }
}

public enum UserRole { Student, Instructor, Admin, OrgAdmin }
public enum TenantPlan { Free, Pro, Enterprise }
```

---

## CourseService — `lms_courses`

```csharp
public class Course : TenantEntity
{
    public Guid InstructorId { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid? ThumbnailContentId { get; set; }
    public string Category { get; set; } = default!;
    public string[] Tags { get; set; } = [];
    public DifficultyLevel Difficulty { get; set; }
    public string Language { get; set; } = "vi";
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public bool IsFree { get; set; } = true;           // ADR-006
    public int Version { get; set; } = 1;              // ADR-005
    public int EnrollmentCount { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public ICollection<CourseSection> Sections { get; set; } = [];
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = [];
}

public class CourseSection : TenantEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public string Title { get; set; } = default!;
    public int Order { get; set; }
    public ICollection<CourseLesson> Lessons { get; set; } = [];
}

public class CourseLesson : TenantEntity
{
    public Guid SectionId { get; set; }
    public CourseSection Section { get; set; } = default!;
    public Guid CourseId { get; set; }
    public string Title { get; set; } = default!;
    public Guid ContentItemId { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsFreePreview { get; set; }
    public bool IsOptional { get; set; }               // excluded from completion %
    public int Order { get; set; }
}

public class CoursePrerequisite : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
}

// Snapshot on every publish — students pin to this (ADR-005)
public class CourseSnapshot : TenantEntity
{
    public Guid CourseId { get; set; }
    public int Version { get; set; }
    public string StructureJson { get; set; } = default!;
    public DateTimeOffset SnapshotAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum CourseStatus { Draft, Published }
public enum DifficultyLevel { Beginner, Intermediate, Advanced }
```

---

## ContentService — `lms_content` (MongoDB)

```csharp
// MongoDB document — NOT EF Core
[BsonCollection("content_items")]
public class ContentItem
{
    [BsonId] public ObjectId Id { get; set; }
    public Guid ContentItemId { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UploadedBy { get; set; }
    public string Filename { get; set; } = default!;
    public ContentType Type { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Pending;
    public string S3Key { get; set; } = default!;
    public long SizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? HlsManifestUrl { get; set; }
    public string? CaptionVttUrl { get; set; }
    public string? CaptionLanguage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[BsonCollection("playback_progress")]
public class PlaybackProgress
{
    [BsonId] public ObjectId Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ContentItemId { get; set; }
    public Guid LessonId { get; set; }
    public Guid TenantId { get; set; }
    public int PositionSeconds { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ContentType { Video, Pdf, Scorm, H5p, Image }
public enum ContentStatus { Pending, Processing, Ready, Failed }
```

---

## EnrollmentService — `lms_enrollments`

```csharp
public class Enrollment : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public EnrollmentStatus Status { get; set; }           // Active, Completed, Suspended, Cancelled
    public bool IsFree { get; set; }                       // denormalised from course at enrol time (Phase 1 stub)
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }          // e.g. "course-archived"
}

public class WaitlistEntry : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public int Position { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum EnrollmentStatus { Active = 0, Completed = 1, Suspended = 2, Cancelled = 3 }
```

**Schema:** `enrollments`. **Indexes:**
- Unique `(TenantId, UserId, CourseId)` filtered `WHERE Status = 0` (Active only; prevents duplicate active enrollment).
- `(TenantId, UserId, Status)` for "list user enrollments".
- `(TenantId, CourseId, Status)` for "course enrollment count / list".

**Concurrency:** `xmin` via `.IsRowVersion()` (Postgres row version for optimistic locking).

---

## ProgressService — `lms_progress`

```csharp
public class LessonProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public Guid CourseId { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; }
}

public class CourseProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public float CompletionPercent { get; set; }
    public int LessonsCompleted { get; set; }
    public int TotalRequiredLessons { get; set; }      // IsOptional=false lessons only
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool CourseCompletedEventPublished { get; set; }   // Idempotency gate for CourseCompleted event
}

public enum ProgressStatus { NotStarted, InProgress, Completed }
```

**Schema:** `progress`. **Indexes:**
- `LessonProgress`: Unique `(TenantId, UserId, LessonId)` for one-to-one per student per lesson; `(TenantId, UserId, CourseId)` for listing lessons in a course.
- `CourseProgress`: Unique `(TenantId, UserId, CourseId)` for one-to-one per student per course; `(TenantId, CourseId)` for course analytics.

**Concurrency:** `xmin` via `.IsRowVersion()` (Postgres row version for optimistic locking) on both entities.

**Tenant filter:** Global `HasQueryFilter` on both entities filtering by `_tenantContext.TenantId`.

---

## AssessmentService — `lms_assessment` + Redis

```csharp
public class Assessment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid? LessonId { get; set; }                // null = course exam (ADR-007)
    public string Title { get; set; } = default!;
    public float PassingScore { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public bool IsRandomised { get; set; }
    public int? QuestionSampleSize { get; set; }
    public bool IsAdaptive { get; set; }               // Phase 2 IRT
    public bool IsActive { get; set; } = true;         // flipped false on CourseArchived
    public ICollection<Question> Questions { get; set; } = [];
}

public class Question : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = default!;
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = default!;
    public QuestionOption[] Options { get; set; } = [];
    public int CorrectOptionIndex { get; set; }
    public string? Explanation { get; set; }
    public int Points { get; set; } = 1;
    public float DifficultyRating { get; set; } = 3f;  // 1–5, Phase 2 IRT
    public int Order { get; set; }
}

public record QuestionOption(string Text);

public class AssessmentAttempt : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public float Score { get; set; }
    public float MaxScore { get; set; }
    public bool Passed { get; set; }
    public int TimeTakenSeconds { get; set; }
    public GradingStatus GradingStatus { get; set; } = GradingStatus.AutoGraded;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ICollection<AttemptAnswer> Answers { get; set; } = [];
}

public class AttemptAnswer : TenantEntity
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public int? SelectedOptionIndex { get; set; }
    public string? TextAnswer { get; set; }            // Phase 2 essays
    public bool? IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
}

// Stored as JSON in Redis — key: assessment:session:{sessionId}
// TTL: timeLimitSeconds + 60s
public class AssessmentSession
{
    public Guid SessionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid[] QuestionIds { get; set; } = [];
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public enum QuestionType { Mcq, TrueFalse, ShortAnswer, Essay }
public enum GradingStatus { AutoGraded, PendingReview, Released }
```

---

## CertificateService — `lms_certificate`

```csharp
public class Certificate : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public string CertificateNumber { get; set; } = default!;  // CERT-{yyyyMM}-{8-char hex}
    public Guid VerificationCode { get; set; }
    public string? PdfStorageKey { get; set; }  // Azure Blob Storage key
    public CertificateStatus Status { get; set; }  // Active, Revoked
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public string? CourseName { get; set; }  // denormalized from CourseCompleted event
    public string? LearnerName { get; set; }  // denormalized from CourseCompleted event
}

public enum CertificateStatus { Active, Revoked }
```

**Schema:** `certificates`. **Indexes:**
- Unique `(TenantId, UserId, CourseId)` (one certificate per student per course)
- `(TenantId, VerificationCode)` for public `/verify/{code}` endpoint
- `(TenantId, UserId, Status)` for listing user's active/revoked certificates

---

## DbContext Pattern — copy for each PostgreSQL service

```csharp
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    public Guid CurrentTenantId { get; set; }

    // Example — replace with actual DbSets
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> Sections => Set<CourseSection>();
    public DbSet<CourseLesson> Lessons => Set<CourseLesson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global tenant filter on all TenantEntity descendants
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(TenantEntity).IsAssignableFrom(e.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "e");
            var prop  = Expression.Property(param, nameof(TenantEntity.TenantId));
            var val   = Expression.Property(
                Expression.Constant(this), nameof(CurrentTenantId));
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(Expression.Equal(prop, val), param));
        }

        // Owned entities
        modelBuilder.Entity<Certificate>()
            .OwnsOne(c => c.BlockchainAnchor);

        // Array columns (PostgreSQL)
        modelBuilder.Entity<Course>()
            .Property(c => c.Tags)
            .HasColumnType("text[]");
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Auto-update UpdatedAt
        foreach (var entry in ChangeTracker.Entries<TenantEntity>()
            .Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;

        return base.SaveChangesAsync(ct);
    }
}
```
