# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.AssessmentService** (Phase 1, service #7)
Port: **5107** · Database: PostgreSQL `lms-assessment` · Schema: `assessments` · Redis (session cache)
ADRs: ADR-007 (Assessment.LessonId nullable — null = course exam, non-null = lesson quiz),
tenant-isolation (EF global filter), event-driven cross-service comms (no direct HTTP),
ADR-004 reference (ProgressService owns completion %, not AssessmentService).

> AssessmentService follows the standard 4-project layout
> (Domain / Infrastructure / Api / Migrator) — Postgres via EF Core,
> with an additional Redis client used by Api/Infrastructure for session
> state.

> **Port note:** `docs/assessment.md` shows `5106`, but `5106` is taken
> by ProgressService (locked previously). AssessmentService shifts to
> `5107`. Flagged as Open Decision #1.

> **Gateway:** YARP route `/api/assessments/{**rest}` → cluster
> `assessment` already exists in `src/gateway/LMS.Gateway/appsettings.json`.
> No gateway-config change required for Phase 1.

---

## 1. Project layout (locked — 4 projects)

```
src/services/LMS.AssessmentService/
  LMS.AssessmentService.Domain/          # entities, enums, ITenantContext, DTO records, validators contract
  LMS.AssessmentService.Infrastructure/  # AssessmentDbContext, repos, MassTransit consumers, Redis session store, DI extensions
  LMS.AssessmentService.Api/             # Minimal API endpoints, validators, Program.cs, header tenancy
  LMS.AssessmentService.Migrator/        # IHostedService running EF Core migrations once on startup
```

References:
- Domain → `LMS.SharedKernel`
- Infrastructure → Domain, `LMS.Contracts`, `LMS.ServiceDefaults`
- Api → Infrastructure, `LMS.ServiceDefaults`
- Migrator → Infrastructure, `LMS.ServiceDefaults`

NuGet (Infrastructure): `Microsoft.EntityFrameworkCore`,
`Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit`,
`MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`,
`StackExchange.Redis` (or `Aspire.StackExchange.Redis` registered in Api).

---

## 2. Entities (LMS.AssessmentService.Domain)

Match `docs/entities.md` exactly. **`Assessment.LessonId` is nullable** —
null = course-level exam, non-null = lesson quiz (CLAUDE.md absolute rule).

```csharp
public class Assessment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid? LessonId { get; set; }                // null => course exam (ADR-007)
    public string Title { get; set; } = default!;
    public float PassingScore { get; set; }
    public int? TimeLimitSeconds { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public bool IsRandomised { get; set; }
    public int? QuestionSampleSize { get; set; }
    public bool IsAdaptive { get; set; }               // Phase 2 IRT
    public bool IsActive { get; set; } = true;         // flipped to false on CourseArchived
    public ICollection<Question> Questions { get; set; } = [];
}

public class Question : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = default!;
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = default!;
    public QuestionOption[] Options { get; set; } = [];   // jsonb
    public int CorrectOptionIndex { get; set; }
    public string? Explanation { get; set; }
    public int Points { get; set; } = 1;
    public float DifficultyRating { get; set; } = 3f;
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

// Stored in Redis (NOT EF) — key: assessment:session:{sessionId}
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

### EF Core configuration (`AssessmentDbContext.OnModelCreating`)
- Schema `assessments`. Tables: `assessments`, `questions`,
  `assessment_attempts`, `attempt_answers` (snake_case).
- Global query filter on `TenantId` for all four entities (auto-applied
  via reflection over `TenantEntity` descendants).
- `Assessment.LessonId` mapped nullable; index includes it.
- `Question.Options` stored as `jsonb` via value converter
  (List<QuestionOption>).
- Concurrency token on all entities: `xmin` via `.IsRowVersion()`.
- Cascade: `Assessment` → `Question` (delete cascade);
  `AssessmentAttempt` → `AttemptAnswer` (delete cascade).
- Conversions: enums stored as `int`.

**Indexes:**
- `Assessment`: `(TenantId, CourseId, LessonId)` — list assessments
  for a course / lesson; `LessonId IS NULL` filtered partial index for
  course-exam lookup.
- `Question`: `(TenantId, AssessmentId, Order)`.
- `AssessmentAttempt`: `(TenantId, UserId, AssessmentId)`,
  `(TenantId, AssessmentId, SubmittedAt DESC)` for analytics.
- `AttemptAnswer`: `(TenantId, AttemptId)`.

### Migration
- Filename: `20260427_001_InitialAssessmentSchema`
- Creates schema `assessments`, four tables with indexes above, plus
  MassTransit outbox/inbox/state tables in `assessments` schema.

---

## 3. Events — `LMS.Contracts/Assessment/`

### Already locked in `docs/events.md` — do NOT modify shape

```csharp
// Published by AssessmentService
public record AssessmentSubmitted(
    Guid UserId, Guid AssessmentId, Guid CourseId, Guid TenantId,
    float Score, bool Passed, DateTimeOffset OccurredAt);

// Phase 2 — declared now, NOT published in Phase 1
public record EssaySubmitted(
    Guid UserId, Guid SubmissionId, Guid AssessmentId,
    Guid TenantId, DateTimeOffset OccurredAt);
```

`src/LMS.Contracts/Assessment/` does NOT yet exist. T1 (events-architect)
creates the folder and at minimum:

- `src/LMS.Contracts/Assessment/AssessmentSubmitted.cs` (Phase 1 publish)
- `src/LMS.Contracts/Assessment/EssaySubmitted.cs` (Phase 2 placeholder, declared only)

> `AssessmentSubmitted` carries no `EventId` — matches the
> `LessonCompleted` / `UserEnrolled` precedent.
> Idempotency key for downstream consumers (ProgressService,
> GamificationService Phase 2): `(UserId, AssessmentId, SubmittedAt)`.

### Events consumed (wired in Infrastructure)

| Event | Source | Behaviour |
|---|---|---|
| `CourseArchived` | CourseService | Set `Assessment.IsActive = false` for all assessments where `CourseId == msg.CourseId` (within tenant filter). Idempotent on `EventId`. New sessions on inactive assessments → `409 ASSESSMENT_INACTIVE`. |
| `GdprErasureRequested` | (future) | NOT wired Phase 1 — flagged Open Decision #2 (despite `docs/assessment.md` listing it). |
| `EnrollmentCancelled` | EnrollmentService | NOT consumed Phase 1 — attempts retained for audit. Flagged Open Decision #3. |

### Publish flow
- `POST /api/assessments/sessions/{sid}/submit` → grade, persist
  `AssessmentAttempt` + `AttemptAnswer` rows, delete Redis session, publish
  `AssessmentSubmitted` via outbox in same transaction.

---

## 4. MassTransit + outbox

- `AddEntityFrameworkOutbox<AssessmentDbContext>` — outbox/inbox/state
  tables generated by EF migration into `assessments` schema.
- `CourseArchivedConsumer` registered with retry `Intervals(1s, 5s, 30s)`
  then poison.
- Inbox idempotency: `CourseArchived` keyed on `EventId`.

---

## 5. Redis session store

- Key: `assessment:session:{sessionId}` (string, JSON-serialised
  `AssessmentSession`).
- TTL: `Assessment.TimeLimitSeconds + 60` seconds; if
  `TimeLimitSeconds == null` use a default of `3600` seconds (Open
  Decision #4).
- Aspire-injected connection via `ConnectionStrings:redis`.
- Deleted on submit (idempotent — second submit returns 410 GONE /
  `SESSION_EXPIRED`).

---

## 6. HTTP endpoints (LMS.AssessmentService.Api)

All routes mounted under `/api/assessments`. Headers: `X-User-Id`,
`X-Tenant-Id`, `X-Roles`. Missing tenant → `400 TENANT_REQUIRED`.

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| POST   | `/api/assessments` | role: `instructor` or `admin` | `CreateAssessmentRequest` | 201 `AssessmentDto` + `Location` | 400, 401, 403 |
| GET    | `/api/assessments/{id}` | tenant-scoped | — | 200 `AssessmentDto` (NO `CorrectOptionIndex`/`Explanation` for students) | 404 |
| GET    | `/api/assessments?courseId={cid}&lessonId={lid?}` | tenant-scoped | — | 200 `AssessmentDto[]` | 400 |
| POST   | `/api/assessments/{id}/questions` | role: `instructor`/`admin` | `CreateQuestionRequest` | 201 `QuestionDto` | 400, 403, 404 |
| PUT    | `/api/assessments/{id}/questions/{qid}` | role: `instructor`/`admin` | `UpdateQuestionRequest` | 200 `QuestionDto` | 400, 403, 404 |
| DELETE | `/api/assessments/{id}/questions/{qid}` | role: `instructor`/`admin` | — | 204 | 403, 404 |
| POST   | `/api/assessments/{id}/sessions` | role: `student` | — | 201 `SessionStartedDto` (questions WITHOUT correct answers) | 403 MAX_ATTEMPTS_EXCEEDED, 404, 409 ASSESSMENT_INACTIVE |
| POST   | `/api/assessments/sessions/{sid}/submit` | role: `student` (own) | `SubmitAnswersRequest` | 200 `AssessmentResultDto` (with correct answers + explanations) | 400, 404 SESSION_NOT_FOUND, 410 SESSION_EXPIRED |
| GET    | `/api/assessments/{id}/attempts/me` | role: `student` | — | 200 `AttemptSummaryDto[]` | — |
| GET    | `/api/assessments/{id}/analytics` | role: `instructor`/`admin` (course-owner) | — | 200 `AssessmentAnalyticsDto` | 403 |

**Out of scope for Phase 1 (in `docs/assessment.md` but deferred):**
`GET /grading/queue`, `POST /grading/{id}/approve`,
`POST /grading/{id}/override`, `POST /attempts/{id}/appeal`
(all Phase 2 essay grading).

### Authorization rules
- All session/submit/me routes scope `UserId` from `X-User-Id`.
- Instructor/admin routes also require tenant scope; cross-tenant →
  empty result (leak-safe via global filter).
- Student GET on `/api/assessments/{id}` strips
  `CorrectOptionIndex`/`Explanation` from each `QuestionDto`. Instructor/admin
  variant returns full payload.

### DTOs (in Domain)

```csharp
public record CreateAssessmentRequest(
    Guid CourseId, Guid? LessonId, string Title, float PassingScore,
    int? TimeLimitSeconds, int MaxAttempts, bool IsRandomised,
    int? QuestionSampleSize);

public record CreateQuestionRequest(
    QuestionType Type, string Prompt, string[] Options,
    int CorrectOptionIndex, string? Explanation, int Points, int Order);

public record UpdateQuestionRequest(
    string Prompt, string[] Options, int CorrectOptionIndex,
    string? Explanation, int Points, int Order);

public record SubmitAnswersRequest(
    IReadOnlyList<SubmittedAnswer> Answers);

public record SubmittedAnswer(
    Guid QuestionId, int? SelectedOptionIndex, string? TextAnswer);

public record QuestionDto(
    Guid Id, QuestionType Type, string Prompt, string[] Options,
    int? CorrectOptionIndex, string? Explanation, int Points, int Order);

public record AssessmentDto(
    Guid Id, Guid CourseId, Guid? LessonId, string Title,
    float PassingScore, int? TimeLimitSeconds, int MaxAttempts,
    bool IsRandomised, int? QuestionSampleSize, bool IsActive,
    IReadOnlyList<QuestionDto> Questions,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record SessionStartedDto(
    Guid SessionId, Guid AssessmentId, DateTimeOffset StartedAt,
    DateTimeOffset? ExpiresAt, IReadOnlyList<QuestionDto> Questions);

public record AssessmentResultDto(
    Guid AttemptId, float Score, float MaxScore, bool Passed,
    int TimeTakenSeconds, IReadOnlyList<GradedAnswerDto> Answers);

public record GradedAnswerDto(
    Guid QuestionId, int? SelectedOptionIndex, int CorrectOptionIndex,
    bool? IsCorrect, int PointsAwarded, string? Explanation);

public record AttemptSummaryDto(
    Guid AttemptId, float Score, float MaxScore, bool Passed,
    DateTimeOffset SubmittedAt);

public record AssessmentAnalyticsDto(
    Guid AssessmentId, int AttemptCount, int PassCount,
    float AvgScore, float AvgTimeSeconds,
    IReadOnlyList<QuestionStatDto> QuestionStats);

public record QuestionStatDto(
    Guid QuestionId, int CorrectCount, int IncorrectCount,
    float CorrectRate);
```

### Error shape
RFC7807 `ProblemDetails`. Codes (`extensions.code`):
`TENANT_REQUIRED`, `ASSESSMENT_INACTIVE`, `MAX_ATTEMPTS_EXCEEDED`,
`SESSION_NOT_FOUND`, `SESSION_EXPIRED`, `INVALID_QUESTION_INDEX`,
`VALIDATION_FAILED`.

### Submit semantics (locked)
1. Load `AssessmentSession` from Redis by `sid` → 404 if missing.
2. Verify `Session.UserId == X-User-Id` and
   `Session.TenantId == X-Tenant-Id` → 404 (leak-safe).
3. For each `QuestionId` in session, grade by type:
   - `Mcq` / `TrueFalse`: `IsCorrect = SelectedOptionIndex == Question.CorrectOptionIndex`,
     `PointsAwarded = IsCorrect ? Points : 0`.
   - `ShortAnswer`: Phase 1 — `IsCorrect = null`, `PointsAwarded = 0`,
     `GradingStatus = PendingReview`.
   - `Essay`: Phase 2 — Phase 1 stub, same as ShortAnswer; do NOT
     publish `EssaySubmitted` in Phase 1.
4. `Score = sum(PointsAwarded)`, `MaxScore = sum(Question.Points)`,
   `Passed = (Score / MaxScore) * 100 >= Assessment.PassingScore`.
5. Persist `AssessmentAttempt` + `AttemptAnswer[]` in single
   transaction; outbox-publish `AssessmentSubmitted`.
6. Delete Redis session (best-effort, idempotent).
7. Return `AssessmentResultDto` with correct answers + explanations.

---

## 7. AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`:

```csharp
var assessmentDb = postgres.AddDatabase("lms-assessment");

var assessmentMigrator = builder.AddProject<Projects.LMS_AssessmentService_Migrator>("assessment-migrator")
    .WithReference(assessmentDb)
    .WaitFor(assessmentDb);

var assessment = builder.AddProject<Projects.LMS_AssessmentService_Api>("assessment")
    .WithReference(assessmentDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitForCompletion(assessmentMigrator);

gateway.WithReference(assessment);
```

Aspire resource name **must** be `assessment` to match the existing
YARP cluster destination `http://assessment`.

### Gateway route (already present)
`src/gateway/LMS.Gateway/appsettings.json` already contains the
`assessment` route + cluster — NO change required.

---

## 8. Configuration keys

```
ConnectionStrings:lms-assessment   (Aspire-injected Postgres)
ConnectionStrings:rabbitmq         (Aspire-injected)
ConnectionStrings:redis            (Aspire-injected)
Assessment:DefaultSessionTtlSeconds = 3600  (fallback when TimeLimitSeconds is null)
```

`ASPNETCORE_URLS` port `5107` in Api `launchSettings.json`; Aspire
overrides at runtime.

---

## 9. Tests (locked surface)

Unit (Domain / Api test projects):
- DTO mappings; student-view strips `CorrectOptionIndex` + `Explanation`.
- `CreateAssessmentRequest` validator: `PassingScore` ∈ [0,100],
  `MaxAttempts` ≥ 1, `Title` non-empty, `(CourseId)` non-empty;
  `LessonId` may be null.
- `CreateQuestionRequest` validator: `CorrectOptionIndex` ∈
  `[0, Options.Length)` else `INVALID_QUESTION_INDEX`.
- Grading helper: MCQ correct/incorrect, score/pass calculation,
  ShortAnswer marked `PendingReview`.

Integration (`LMS.IntegrationTests/AssessmentService/`) — Testcontainers
Postgres + Redis + RabbitMQ + MassTransit harness:
- Tenant isolation pair: tenant A cannot read tenant B's assessments,
  questions, attempts (404 / empty).
- Create assessment with `LessonId = null` (course exam) and with
  non-null `LessonId` (lesson quiz) — both persist.
- Start session: questions returned WITHOUT `CorrectOptionIndex`;
  Redis key created with TTL.
- Submit session: grades correctly, persists attempt, publishes
  `AssessmentSubmitted` exactly once, deletes Redis session.
- Re-submit same session → 404 SESSION_NOT_FOUND (idempotent — no
  duplicate `AssessmentSubmitted`).
- `MaxAttempts` exceeded → 403 MAX_ATTEMPTS_EXCEEDED.
- `CourseArchivedConsumer` flips `IsActive = false` idempotently;
  starting a new session on inactive assessment → 409.
- Student GET hides correct answers; instructor GET reveals them.

Architecture (`LMS.ArchitectureTests`):
- All four entities inherit `TenantEntity`.
- `AssessmentDbContext` applies global query filter on all four.
- Api project does NOT reference `Microsoft.AspNetCore.Authentication.JwtBearer`
  (gateway-only JWT rule).

Contract (`LMS.ContractTests`):
- `AssessmentSubmitted` shape exactly `(UserId, AssessmentId, CourseId,
  TenantId, Score, Passed, OccurredAt)`.
- `EssaySubmitted` record present (Phase 2 placeholder) with shape
  `(UserId, SubmissionId, AssessmentId, TenantId, OccurredAt)`.

---

## 10. Open decisions (auto-resolved — flagged for human review)

1. **Port 5107** chosen because 5106 is already ProgressService.
   `docs/assessment.md` shows 5106 (stale — same conflict pattern as
   ProgressService had with EnrollmentService); doc fix in T11.
2. **`GdprErasureRequested` not wired Phase 1** — listed in
   `docs/assessment.md` consumed list, deferred to a global GDPR sprint
   (same stance as ProgressService).
3. **`EnrollmentCancelled` not consumed** — attempts retained for
   audit / reporting; cancellation does not erase records.
4. **Default session TTL when `TimeLimitSeconds == null` = 3600s.**
   `docs/assessment.md` only specifies TTL for timed assessments;
   needs explicit fallback for untimed exams. Configurable via
   `Assessment:DefaultSessionTtlSeconds`.
5. **`ShortAnswer` grading deferred** — Phase 1 marks attempt
   `GradingStatus = PendingReview` and awards 0 points. UI must show
   "pending review" state; no `EssaySubmitted` emitted Phase 1.
6. **No CourseService callout for course/lesson existence** — direct
   HTTP forbidden; relies on FK-less GUIDs and tenant filter.
   Misaligned `CourseId` is accepted at create time (Open Decision —
   acceptable per ADR no-cross-service-HTTP).
7. **`AssessmentSubmitted` -> ProgressService consumer** is
   ProgressService's responsibility (already-flagged in ProgressService
   contracts as deferred). Phase 1 publishes the event regardless;
   ProgressService can wire later without contract change.
