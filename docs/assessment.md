# AssessmentService

**Port:** 5106 | **DB:** `lms_assessment` (PostgreSQL) + Redis | **ADRs:** ADR-007

## Responsibility
Quiz engine with timed sessions (Redis), question bank, and auto-grading.
Correct answers never returned until after submission.
Phase 2 extends with adaptive IRT and LLM essay grading.

## Endpoints

```
POST /api/assessments                              ← CreateAssessmentRequest
GET  /api/assessments/{id}
POST /api/assessments/{id}/questions               ← CreateQuestionRequest
PUT  /api/assessments/{id}/questions/{qid}
DELETE /api/assessments/{id}/questions/{qid}
POST /api/assessments/{id}/sessions                → AssessmentSession      # start quiz
POST /api/assessments/sessions/{sid}/submit        ← SubmitAnswersRequest  → AssessmentResult
GET  /api/assessments/{id}/attempts/me             → AttemptSummary[]
GET  /api/assessments/{id}/analytics               → AssessmentAnalytics    # instructor
# Phase 2 grading stubs:
GET  /api/assessments/grading/queue
POST /api/assessments/grading/{subId}/approve
POST /api/assessments/grading/{subId}/override
POST /api/assessments/attempts/{id}/appeal
```

## Key flows

**Start session:**
1. Check attempt count < `MaxAttempts` → 403 if exceeded
2. Sample questions if `IsRandomised` + `QuestionSampleSize`
3. Store `AssessmentSession` JSON in Redis (key: `assessment:session:{sessionId}`)
4. TTL = `timeLimitSeconds + 60` seconds
5. Return questions WITHOUT `CorrectOptionIndex`

**Submit:**
1. Load session from Redis → 404 if expired
2. Grade MCQ/TrueFalse: compare selected vs correct
3. Calculate score, pass/fail
4. Save `AssessmentAttempt` + `AttemptAnswer` records
5. Delete session from Redis
6. Publish `AssessmentSubmitted`
7. Return result WITH correct answers and explanations

## Events published
- `AssessmentSubmitted`
- `EssaySubmitted` (Phase 2 — when QuestionType is Essay)

## Events consumed
- `GdprErasureRequested` — anonymise attempt records

## Entities
See `docs/entities.md` → AssessmentService section.
