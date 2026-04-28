# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.ProgressService** (Phase 1, service #6)
Port: **5106** · Database: PostgreSQL `lms-progress` · Schema: `progress`
ADRs: ADR-004 (ProgressService owns completion %; ContentService owns
resume position), tenant-isolation (EF global filter), event-driven cross
service comms (no direct HTTP).

> ProgressService follows the standard 4-project layout
> (Domain / Infrastructure / Api / Migrator) — Postgres via EF Core.

> **Port note:** `docs/progress.md` shows `5105`, but `5105` is already
> taken by EnrollmentService (locked in prior contracts). ProgressService
> shifts to `5106`. Flagged as Open Decision #1.

---

## 1. Project layout (locked — 4 projects)

```
src/services/LMS.ProgressService/
  LMS.ProgressService.Domain/          # entities, enums, ITenantContext, DTO records
  LMS.ProgressService.Infrastructure/  # ProgressDbContext, repos, MassTransit consumers, DI extensions
  LMS.ProgressService.Api/             # Minimal API endpoints, validators, Program.cs, header tenancy
  LMS.ProgressService.Migrator/        # IHostedService running EF Core migrations once on startup
```

References:
- Domain → `LMS.SharedKernel`
- Infrastructure → Domain, `LMS.Contracts`, `LMS.ServiceDefaults`
- Api → Infrastructure, `LMS.ServiceDefaults`
- Migrator → Infrastructure, `LMS.ServiceDefaults`

NuGet (Infrastructure): `Microsoft.EntityFrameworkCore`,
`Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit`,
`MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`.

---

## 2. Entities (LMS.ProgressService.Domain)

Match `docs/entities.md` exactly:

```csharp
public class LessonProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public Guid CourseId { get; set; }
    public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
    public float WatchPercent { get; set; }              // 0–100
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
}

public class CourseProgress : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public float CompletionPercent { get; set; }
    public int LessonsCompleted { get; set; }
    public int TotalRequiredLessons { get; set; }        // IsOptional=false lessons only
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum ProgressStatus { NotStarted, InProgress, Completed }
```

> `TimeSpentSeconds` is intentionally NOT modelled in Phase 1
> (resume position belongs to ContentService.PlaybackProgress per ADR-004).
> Flagged as Open Decision #2.

### EF Core configuration (`ProgressDbContext.OnModelCreating`)
- Schema `progress`. Tables: `lesson_progress`, `course_progress`.
- Global query filter on `TenantId` for both entities (auto-applied via
  reflection over `TenantEntity` descendants — same pattern as other
  services).
- Indexes:
  - `LessonProgress`: unique `(TenantId, UserId, LessonId)`.
  - `LessonProgress`: `(TenantId, UserId, CourseId, Status)` for
    "lessons of a course for a user".
  - `CourseProgress`: unique `(TenantId, UserId, CourseId)`.
  - `CourseProgress`: `(TenantId, CourseId)` for instructor analytics.
- Concurrency token on both entities: `xmin` via `.IsRowVersion()`.
- Conversions: enums stored as `int`.

### Migration
- Filename: `20260427_001_InitialProgressSchema`
- Creates schema `progress`, both tables with indexes above, plus
  MassTransit outbox/inbox tables in `progress` schema.

---

## 3. Events — `LMS.Contracts/Progress/`

### Already locked in `docs/events.md` — do NOT modify shape

```csharp
// Published by ProgressService
public sealed record LessonCompleted(
    Guid UserId, Guid LessonId, Guid CourseId,
    Guid TenantId, float WatchPercent, DateTimeOffset OccurredAt);

public sealed record CourseCompleted(
    Guid UserId, Guid CourseId, Guid TenantId,
    DateTimeOffset OccurredAt);
```

Both records currently DO NOT exist as physical files in `src/LMS.Contracts/`
(verified: only `Content/`, `Course/`, `Enrollment/`, `Identity/` exist).
T1 (events-architect) creates them at:

- `src/LMS.Contracts/Progress/LessonCompleted.cs`
- `src/LMS.Contracts/Progress/CourseCompleted.cs`

> Neither event carries `EventId` — matches `UserEnrolled` precedent.
> Idempotency keys: `LessonCompleted` → `(UserId, LessonId)`;
> `CourseCompleted` → `(UserId, CourseId)`. Consumers
> (CertificateService, NotificationWorker, GamificationService Phase 2)
> dedupe on those natural keys.

### Events consumed (all wired in Infrastructure)

| Event | Source | Behaviour |
|---|---|---|
| `UserEnrolled` | EnrollmentService | Upsert empty `CourseProgress` for `(UserId, CourseId)`. `TotalRequiredLessons = 0` initially (set on first lesson interaction or via course-snapshot lookup — Phase 1 stub: leaves at 0 and updates lazily on first `LessonCompleted` flow). Idempotent on `(UserId, CourseId)`. |
| `EnrollmentCancelled` | EnrollmentService | Soft-freeze: mark `CourseProgress.LastAccessedAt = OccurredAt`; leaves rows intact for re-enrol. NO destructive update. Phase 1 stub. Idempotent on `EventId`. |
| `CourseArchived` | CourseService | NOT consumed in Phase 1 (informational only — flagged as Open Decision #3). |
| `GdprErasureRequested` | (future) | NOT wired Phase 1 — flagged as Open Decision #4. |
| `PlaybackProgressReported` | ContentService | NOT wired Phase 1 (event does not exist in `LMS.Contracts` yet — `docs/progress.md` references it, but it is not in `docs/events.md`). Flagged as Open Decision #5. |
| `AssessmentSubmitted` | AssessmentService | `docs/events.md` lists ProgressService as consumer, but Phase 1 routing for "lesson quiz pass = lesson complete" is out-of-scope for this sprint. Flagged as Open Decision #6. |

### Publish flow
- `POST /api/progress/lessons/{lessonId}/complete` → upsert `LessonProgress`,
  recompute `CourseProgress`, publish `LessonCompleted` via outbox.
  If `CourseProgress.CompletionPercent >= 100`, also publish
  `CourseCompleted` via outbox in same transaction.

---

## 4. MassTransit + outbox

- `AddEntityFrameworkOutbox<ProgressDbContext>` — outbox/inbox/state tables
  generated by EF migration into `progress` schema.
- `UserEnrolledConsumer` and `EnrollmentCancelledConsumer` registered with
  retry `Intervals(1s, 5s, 30s)` then poison.
- Inbox idempotency: `UserEnrolled` keyed on `(UserId, CourseId)`;
  `EnrollmentCancelled` keyed on `EventId`.

---

## 5. HTTP endpoints (LMS.ProgressService.Api)

All routes mounted under `/api/progress`. Gateway YARP cluster `progress`
must be added (T7 AppHost task wires the project as `progress`; gateway
route addition is part of T6 Api task — `appsettings.json` route
`/api/progress/{**rest}` → `progress` cluster).

Headers: `X-User-Id`, `X-Tenant-Id`, `X-Roles`. Missing tenant → `400 TENANT_REQUIRED`.

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| POST | `/api/progress/lessons/{lessonId}/complete` | role: `student` (own progress) | `CompleteLessonRequest { Guid CourseId, float WatchPercent }` | 200 `LessonProgressDto` | 400, 401, 404 LESSON_NOT_FOUND_FOR_USER |
| GET  | `/api/progress/courses/{courseId}` | tenant scope (own) | — | 200 `CourseProgressDto` | 404 |
| GET  | `/api/progress/courses/{courseId}/lessons` | tenant scope (own) | — | 200 `LessonProgressDto[]` | — |
| GET  | `/api/progress/me` | role: `student` | — | 200 `CourseProgressSummaryDto[]` | — |
| GET  | `/api/progress/courses/{courseId}/analytics` | role: `instructor` (of course) or `admin` | — | 200 `LessonAnalyticsDto[]` | 403 |

**Out of scope for Phase 1** (in `docs/progress.md` but deferred):
`POST /api/progress/sync` (PWA offline batch — Phase 3).

### Authorization rules
- All `*me*` and self routes scope `UserId` from `X-User-Id`.
- Analytics endpoint requires `instructor` or `admin` role; cross-tenant →
  empty result (leak-safe: query returns zero rows under tenant filter).

### DTOs (in Domain)
```csharp
public record CompleteLessonRequest(Guid CourseId, float WatchPercent);

public record LessonProgressDto(
    Guid Id, Guid UserId, Guid LessonId, Guid CourseId,
    ProgressStatus Status, float WatchPercent,
    DateTimeOffset? CompletedAt, DateTimeOffset? LastAccessedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CourseProgressDto(
    Guid Id, Guid UserId, Guid CourseId,
    float CompletionPercent, int LessonsCompleted, int TotalRequiredLessons,
    DateTimeOffset? LastAccessedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CourseProgressSummaryDto(
    Guid CourseId, float CompletionPercent,
    DateTimeOffset? LastAccessedAt, DateTimeOffset? CompletedAt);

public record LessonAnalyticsDto(
    Guid LessonId, int CompletedCount, int InProgressCount,
    float AvgWatchPercent);
```

### Error shape
RFC7807 `ProblemDetails`. Codes (`extensions.code`):
`TENANT_REQUIRED`, `LESSON_NOT_FOUND_FOR_USER`, `INVALID_WATCH_PERCENT`.

### Completion-recompute semantics (locked)
On `POST .../complete`:
1. Upsert `LessonProgress { Status=Completed, WatchPercent (clamped 0–100),
   CompletedAt=now, LastAccessedAt=now }`. Idempotent: re-completing is a
   no-op for the publish step (already-completed → return 200 without
   re-publishing).
2. Recompute `CourseProgress`:
   - `LessonsCompleted` = count of `LessonProgress` where
     `(UserId, CourseId, Status=Completed)`.
   - `TotalRequiredLessons` is updated **only when greater than current**
     (Phase 1 stub: lesson totals are not authoritative without a
     CourseService callout — see Open Decision #7). Use
     `max(stored, lessonsCompleted)` to keep `CompletionPercent` ≤ 100.
   - `CompletionPercent = LessonsCompleted / max(1, TotalRequiredLessons) * 100`.
   - `CompletedAt = now` if reached 100% for the first time.
3. Publish `LessonCompleted` via outbox.
4. If transitioned to 100% in this same transaction → publish `CourseCompleted`.

---

## 6. AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`:

```csharp
var progressDb = postgres.AddDatabase("lms-progress");

var progressMigrator = builder.AddProject<Projects.LMS_ProgressService_Migrator>("progress-migrator")
    .WithReference(progressDb)
    .WaitFor(progressDb);

var progress = builder.AddProject<Projects.LMS_ProgressService_Api>("progress")
    .WithReference(progressDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(progressMigrator);

gateway.WithReference(progress);
```

Aspire resource name **must** be `progress` to match the YARP cluster
destination `http://progress`.

### Gateway route (T6 — Api task touches gateway config)
Add to `src/gateway/LMS.Gateway/appsettings.json`:
```json
"progress": { "ClusterId": "progress", "Match": { "Path": "/api/progress/{**rest}" } }
```
Cluster:
```json
"progress": { "Destinations": { "d1": { "Address": "http://progress" } } }
```

---

## 7. Configuration keys

```
ConnectionStrings:lms-progress    (Aspire-injected Postgres)
ConnectionStrings:rabbitmq        (Aspire-injected)
```

`ASPNETCORE_URLS` port `5106` in Api `launchSettings.json`; Aspire overrides at runtime.

---

## 8. Tests (locked surface)

Unit (Api / Domain test projects):
- DTO mappings (Lesson → LessonProgressDto, Course → CourseProgressDto).
- `CompleteLessonRequest` validator: `CourseId` non-empty,
  `WatchPercent` in `[0, 100]` else `INVALID_WATCH_PERCENT`.
- Recompute helper: 0-of-N → 0%, all-of-N → 100%, idempotent on re-complete.

Integration (`LMS.IntegrationTests/ProgressService/`) — Testcontainers
Postgres + RabbitMQ + MassTransit harness:
- Tenant isolation pair: tenant A cannot read tenant B's
  `LessonProgress` / `CourseProgress` (404 / empty).
- `POST /api/progress/lessons/{id}/complete` → 200, row persisted with
  `Status=Completed`, publishes `LessonCompleted`.
- Re-complete same lesson → 200, no duplicate `LessonCompleted` emitted.
- Reaching `CompletionPercent=100` publishes `CourseCompleted` exactly once.
- `UserEnrolledConsumer` upserts a `CourseProgress` row idempotently.
- `EnrollmentCancelledConsumer` updates `LastAccessedAt`, leaves
  progress data intact, idempotent on `EventId`.
- `GET /api/progress/courses/{id}/analytics` requires instructor/admin role
  (403 for student).

Architecture (`LMS.ArchitectureTests`):
- `LessonProgress`, `CourseProgress` inherit `TenantEntity`.
- `ProgressDbContext` applies global query filter on both.

Contract (`LMS.ContractTests`):
- `LessonCompleted` shape exactly `(UserId, LessonId, CourseId, TenantId,
  WatchPercent, OccurredAt)`.
- `CourseCompleted` shape exactly `(UserId, CourseId, TenantId, OccurredAt)`.

---

## 9. Open decisions (auto-resolved — flagged for human review)

1. **Port 5106** chosen because 5105 is already EnrollmentService.
   `docs/progress.md` shows 5105 (stale); doc fix lives in T11.
2. **`TimeSpentSeconds` not modelled** — ADR-004 places resume/time
   data in ContentService. ProgressService stays focused on completion.
3. **`CourseArchived` not consumed** — informational only; archived
   courses become unenrollable but existing progress is preserved.
4. **`GdprErasureRequested` not wired Phase 1** — listed in
   `docs/progress.md` but deferred until a global GDPR sprint.
5. **`PlaybackProgressReported` not consumed Phase 1** — event does not
   exist in `docs/events.md`; auto-complete-at-80% is deferred.
6. **`AssessmentSubmitted` not consumed Phase 1** — ProgressService is
   listed as consumer in `docs/events.md`, but Phase 1 keeps lesson
   completion strictly user-driven via the explicit POST endpoint.
7. **`TotalRequiredLessons` is lazy** — set to `max(stored,
   LessonsCompleted)` until Phase 2 introduces a CourseSnapshot lookup
   (no direct cross-service HTTP allowed). Means
   `CompletionPercent` is meaningful only after the first complete.
