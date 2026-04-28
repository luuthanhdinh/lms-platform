# AssessmentService

**Port:** 5107 | **DB:** `lms_assessment` (PostgreSQL, schema `assessments`) + Redis | **ADRs:** ADR-007

## Responsibility

Quiz and exam engine with timed sessions (Redis), randomized question sampling, and auto-grading for MCQ/TrueFalse.
Correct answers are only revealed *after* submission, never during active sessions.
Phase 2 extends with adaptive IRT and LLM essay grading.

## Endpoints

```
POST   /api/assessments                           ← CreateAssessmentRequest
GET    /api/assessments                           → Assessment[]  (filtered by courseId, lessonId)
GET    /api/assessments/{id}
POST   /api/assessments/{id}/questions            ← CreateQuestionRequest
PUT    /api/assessments/{id}/questions/{qid}
DELETE /api/assessments/{id}/questions/{qid}
POST   /api/assessments/{id}/sessions             → SessionStartedDto  (start quiz)
POST   /api/assessments/sessions/{sid}/submit     ← SubmitAnswersRequest  → AssessmentResultDto
GET    /api/assessments/{id}/attempts/me          → AttemptSummaryDto[]
GET    /api/assessments/{id}/analytics            → AssessmentAnalyticsDto  (instructor/admin only)
```

**Auth:** All endpoints require `X-User-Id`, `X-Tenant-Id`, `X-Roles` headers (validated at YARP gateway).

## Key flows

**Create assessment (instructor/admin):**
1. Check authorization: `IsInstructorOrAdmin(ctx)`
2. Accept `CourseId`, optional `LessonId` (null = course-level exam), `Title`, `PassingScore`, `TimeLimitSeconds`, `MaxAttempts`, `IsRandomised`, `QuestionSampleSize`
3. Persist with `IsActive=true`

**Get assessment:**
- Instructors: see all fields including `CorrectOptionIndex` in questions
- Students: see only if `IsActive=true`; questions exclude `CorrectOptionIndex`

**Start session (student only):**
1. Check `IsActive=true` → 409 if deactivated
2. Count prior attempts: if `>= MaxAttempts`, return 409 `MAX_ATTEMPTS_EXCEEDED`
3. Load questions by `Order`, apply randomization if `IsRandomised=true`
4. Sample `QuestionSampleSize` questions if set and `< total`
5. Create `AssessmentSession` JSON in Redis with TTL = `TimeLimitSeconds + 60` (or default 3600s)
6. Return `SessionStartedDto` with questions WITHOUT `CorrectOptionIndex` or `Explanation`

**Submit answers (student only):**
1. Load session from Redis → 404 if expired
2. Verify session ownership: `session.UserId == ctx.UserId && session.TenantId == ctx.TenantId`
3. Grade MCQ/TrueFalse: compare `SelectedOptionIndex` vs `CorrectOptionIndex` → auto-pass/fail
4. Grade ShortAnswer/Essay: set `IsCorrect=null`, mark `GradingStatus=PendingReview`
5. Calculate score: sum `PointsAwarded` / `MaxScore` * 100
6. Determine pass/fail: `score >= Assessment.PassingScore`
7. Persist `AssessmentAttempt` + `AttemptAnswer[]` records (all with `TenantId`)
8. Delete session from Redis (best-effort)
9. Publish `AssessmentSubmitted` event
10. Return `AssessmentResultDto` WITH correct answers and explanations for all questions

**List attempts (student):**
- Show only own attempts: filter by `UserId == ctx.UserId`
- Summary view: `(Id, AssessmentId, Score, MaxScore, Passed, TimeTakenSeconds, SubmittedAt)`

**Analytics (instructor/admin):**
- Aggregate all attempts for assessment: total, passed count, avg % score, avg time
- Per-question stats: correct/incorrect counts, % correct

## Events published

**`AssessmentSubmitted`**
- Published in same transaction as `AssessmentAttempt` save (outbox pattern)
- Shape: `(UserId, AssessmentId, CourseId, TenantId, Score: int [0-100], Passed, OccurredAt)`
- **No EventId field** — dedupe key is `(UserId, AssessmentId, OccurredAt)`
- Consumers: `ProgressService` (lesson-quiz pass → mark lesson complete), `NotificationWorker` (send result email)

## Events consumed

**`CourseArchived`**
- Set all active assessments for that course to `IsActive=false`
- Subsequent student requests return 409 `ASSESSMENT_INACTIVE`
- Idempotent: if already inactive, no-op

## Entities

Schema: `assessments` | **Tenant filter:** Yes (global `HasQueryFilter` on `TenantId`)

### Assessment

```csharp
public class Assessment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid? LessonId { get; set; }          // null = course-level exam (ADR-007)
    public string Title { get; set; }
    public float PassingScore { get; set; }      // 0–100 percentage
    public int? TimeLimitSeconds { get; set; }   // null = no time limit
    public int MaxAttempts { get; set; } = 3;
    public bool IsRandomised { get; set; }
    public int? QuestionSampleSize { get; set; } // null = all questions
    public bool IsAdaptive { get; set; }         // Phase 2: IRT
    public bool IsActive { get; set; } = true;   // flipped false on CourseArchived
    public ICollection<Question> Questions { get; set; }
}
```

**Indexes:**
- `(TenantId, CourseId)` for "list assessments in course"
- `(TenantId, CourseId, LessonId)` filtered `WHERE IsActive=true` for "lesson quiz lookup"

### Question

```csharp
public class Question : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; }
    public QuestionType Type { get; set; }       // Mcq, TrueFalse, ShortAnswer, Essay
    public string Prompt { get; set; }
    public QuestionOption[] Options { get; set; } // stored as JSONB
    public int CorrectOptionIndex { get; set; }  // index into Options[] (never leaked pre-submit)
    public string? Explanation { get; set; }     // shown post-submit
    public int Points { get; set; } = 1;
    public float DifficultyRating { get; set; } = 3f;  // 1–5 (Phase 2: IRT)
    public int Order { get; set; }
}

public record QuestionOption(string Text);
```

**Indexes:**
- `(TenantId, AssessmentId, Order)` for ordered question load

### AssessmentAttempt

```csharp
public class AssessmentAttempt : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public float Score { get; set; }            // points earned
    public float MaxScore { get; set; }         // sum of all question points in attempt
    public bool Passed { get; set; }
    public int TimeTakenSeconds { get; set; }
    public GradingStatus GradingStatus { get; set; } = GradingStatus.AutoGraded;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ICollection<AttemptAnswer> Answers { get; set; }
}

public enum GradingStatus { AutoGraded, PendingReview, Released }
```

**Indexes:**
- `(TenantId, AssessmentId, UserId)` for "list user's attempts on assessment"
- `(TenantId, UserId)` for "all attempts by user"

### AttemptAnswer

```csharp
public class AttemptAnswer : TenantEntity
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public int? SelectedOptionIndex { get; set; }  // MCQ/TrueFalse answer
    public string? TextAnswer { get; set; }        // ShortAnswer/Essay (Phase 2)
    public bool? IsCorrect { get; set; }           // null if pending review
    public int PointsAwarded { get; set; }
}
```

**Indexes:**
- `(TenantId, AttemptId)` for "load answers for attempt"

### AssessmentSession (Redis only, not EF Core)

```csharp
public class AssessmentSession
{
    public Guid SessionId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid[] QuestionIds { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
```

**Storage:** Redis hash/JSON with key `assessment:session:{sessionId}`, TTL = `TimeLimitSeconds + 60` seconds.

## Special behaviors

**Correct-answer leak prevention:**
- During active session: `QuestionOption[]` and `CorrectOptionIndex` never appear in response
- Only `Prompt`, `Type`, and `Options[].Text` included in `SessionStartedDto`
- Post-submit: `AssessmentResultDto` includes full `GradedAnswerDto[]` with `CorrectOptionIndex`, `Explanation`, `IsCorrect`

**ShortAnswer/Essay handling (Phase 1):**
- During submit: set `IsCorrect=null`, `PointsAwarded=0`, mark attempt `GradingStatus=PendingReview`
- Grading queue and manual override endpoints stubbed for Phase 2

**Session expiry:**
- Redis TTL enforced by store
- Expired session → 404 with `SESSION_NOT_FOUND`

**Deactivation flow:**
- When `CourseArchived` event received: flip all active assessments for that course to `IsActive=false`
- Students cannot start new sessions on inactive assessments (409 `ASSESSMENT_INACTIVE`)
- Existing attempts remain readable

## Related ADRs

- **ADR-007:** `LessonId` nullable design; null assessments as course-level exams
