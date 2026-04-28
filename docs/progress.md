# ProgressService

**Port:** 5105 | **DB:** `lms_progress` (PostgreSQL) | **ADRs:** ADR-004

## Responsibility
Lesson completion tracking and course completion calculation.
Owns completion % (ADR-004) — ContentService owns resume position.
Publishes CourseCompleted when 100% required lessons done.

## Endpoints

```
POST /api/progress/lessons/{lessonId}/complete  ← {courseId, watchPercent}
GET  /api/progress/courses/{courseId}           → CourseProgress
GET  /api/progress/courses/{courseId}/lessons   → LessonProgress[]
GET  /api/progress/courses/{courseId}/analytics → LessonAnalytics[]   # instructor
GET  /api/progress/me                           → CourseProgressSummary[]
POST /api/progress/sync                         ← OfflineProgressEvent[] # Phase 3 PWA
```

## Key flows

**Complete lesson (idempotent):**
1. Upsert `LessonProgress {Status=Completed, WatchPercent, CompletedAt}`
2. Count completed required lessons (`IsOptional=false`)
3. Update `CourseProgress.CompletionPercent = completed / total * 100`
4. Publish `LessonCompleted`
5. If `CompletionPercent >= 100`: publish `CourseCompleted`

**Auto-complete from video heartbeat:**
- Consume `PlaybackProgressReported` event
- If `watchPercent >= 80` → treat as lesson complete (same flow as above)

## Events published
- `LessonCompleted`
- `CourseCompleted`

## Events consumed
- `PlaybackProgressReported` (from ContentService) — auto-complete at 80%
- `UserEnrolled` — initialise `CourseProgress` record with `TotalRequiredLessons`
- `GdprErasureRequested` — delete all progress records for user

## Entities
See `docs/entities.md` → ProgressService section.
