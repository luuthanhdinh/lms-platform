# LMS.ProgressService

**Responsibility:** Tracks student progress through lessons and courses. Seeds CourseProgress on enrollment, updates on lesson completion, publishes completion events for certificate issuance and notifications. Freezes progress on cancellation.

**Port / AppHost:** `progress` / port 5106 | **Database:** PostgreSQL `lms_progress`, schema `progress`

**Architecture decisions:** None yet

---

## Entities

All inherit `TenantEntity`. Schema: `progress`.

| Entity | Key fields | Notes |
|---|---|---|
| `LessonProgress` | UserId, LessonId, CourseId, Status, LastAccessedAt, CompletedAt | Unique `(TenantId, UserId, LessonId)` |
| `CourseProgress` | UserId, CourseId, CompletionPercent, LessonsCompleted, TotalRequiredLessons, CompletionPercent, LastAccessedAt, CompletedAt, CourseCompletedEventPublished | Unique `(TenantId, UserId, CourseId)` |

**Enums:** `ProgressStatus` (NotStarted|InProgress|Completed)

**Concurrency:** `xmin` (PostgreSQL row version) via `.IsRowVersion()` on both entities.

**Indexes:**
- `LessonProgress`: `(TenantId, UserId, LessonId)` unique; `(TenantId, UserId, CourseId)` for list
- `CourseProgress`: `(TenantId, UserId, CourseId)` unique; `(TenantId, CourseId)` for analytics

---

## HTTP Endpoints

All routes under `/api/progress`. Headers forwarded by gateway: `X-User-Id`, `X-Tenant-Id`, `X-Roles`.

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/progress/lessons/{lessonId}/complete` | authenticated | Mark lesson complete; upsert CourseProgress; publish LessonCompleted + CourseCompleted (if 100%) |
| `GET` | `/api/progress/courses/{courseId}` | authenticated | Get CourseProgress for current user |
| `GET` | `/api/progress/courses/{courseId}/lessons` | authenticated | List all LessonProgress for course |
| `GET` | `/api/progress/courses/{courseId}/analytics` | instructor/admin | Course-level stats: total enrolled, completed, in-progress, not-started |

**Error codes:** 400 (TENANT_REQUIRED), 401 (UNAUTHORIZED), 403 (FORBIDDEN), 404 (PROGRESS_NOT_FOUND)

---

## Events

### Published (via MassTransit outbox)

```csharp
public sealed record LessonCompleted(
    Guid UserId,
    Guid LessonId,
    Guid CourseId,
    Guid TenantId,
    DateTimeOffset OccurredAt);

public sealed record CourseCompleted(
    Guid UserId,
    Guid CourseId,
    Guid TenantId,
    DateTimeOffset OccurredAt);
```

**Idempotency:**
- `LessonCompleted`: dedupe key `(UserId, LessonId)`; no EventId
- `CourseCompleted`: dedupe key `(UserId, CourseId)`; no EventId; published only once per enrollment via `CourseProgress.CourseCompletedEventPublished` gate

### Consumed

- **`UserEnrolled`** (EnrollmentService) → seeds `CourseProgress` with `LessonsCompleted=0`, `CompletionPercent=0`
- **`EnrollmentCancelled`** (EnrollmentService) → soft-freeze: updates `LastAccessedAt` only (preserves data for re-enrollment)

---

## Completion Flow

1. Client calls `POST /api/progress/lessons/{lessonId}/complete` with `CourseId`
2. Service loads/creates `LessonProgress`
3. If already completed → idempotent 200 OK, no event publish
4. Otherwise → mark complete, upsert/update `CourseProgress`, increment `LessonsCompleted`
5. Calculate `CompletionPercent = LessonsCompleted * 100 / TotalRequiredLessons`
6. Publish `LessonCompleted`
7. If `CompletionPercent >= 100` AND `CourseCompletedEventPublished == false`:
   - Set `CourseCompletedEventPublished = true`
   - Publish `CourseCompleted`
8. Atomically commit all changes + outbox messages

---

## CourseCompletedEventPublished Gate

`CourseProgress.CourseCompletedEventPublished` prevents duplicate event publication on idempotent retries. Set to `true` only once `CompletionPercent >= 100`, before publishing `CourseCompleted`. Cleared only on `EnrollmentCancelled` (soft-freeze doesn't clear; allows re-completion on re-enrollment).

---

## Project Layout

```
LMS.ProgressService/
  Domain/          # Entities, enums, DTOs, repositories, abstractions
  Infrastructure/  # DbContext, EF configs, MassTransit consumers
  Api/             # Minimal APIs, auth, mapping
  Migrator/        # IHostedService for EF Core migrations on startup
```

---

## AppHost Wiring

```csharp
var progressDb = postgres.AddDatabase("lms-progress");
var progressMigrator = builder.AddProject<LMS_ProgressService_Migrator>("progress-migrator")
    .WithReference(progressDb).WaitFor(progressDb);
var progress = builder.AddProject<LMS_ProgressService_Api>("progress")
    .WithReference(progressDb).WithReference(rabbitmq).WaitForCompletion(progressMigrator);
gateway.WithReference(progress);
```

YARP routes `/api/progress/**` → `progress` cluster. Gateway transforms inject trusted headers.
