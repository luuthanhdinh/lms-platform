# CourseService

**Port:** 5102 | **DB:** `lms_courses` (PostgreSQL) | **ADRs:** ADR-005, ADR-006

## Responsibility
Course catalogue hierarchy: Course → Section → Lesson.
Does NOT serve media (ContentService handles that).
GET catalogue endpoints are public (no auth).

## Endpoints

```
GET  /api/courses                                      → CourseSummary[]   # public
POST /api/courses                                      ← CreateCourseRequest
GET  /api/courses/{id}                                 → CourseDetail      # public
PUT  /api/courses/{id}                                 ← UpdateCourseRequest
POST /api/courses/{id}/publish                                             # instructor
POST /api/courses/{id}/unpublish
POST /api/courses/{id}/duplicate
GET  /api/courses/{id}/syllabus                        → CourseSyllabus    # public

POST /api/courses/{id}/sections                        ← SectionRequest
PUT  /api/courses/{id}/sections/{sid}                  ← SectionRequest
DELETE /api/courses/{id}/sections/{sid}

POST /api/courses/{id}/sections/{sid}/lessons          ← CreateLessonRequest
PUT  /api/courses/{id}/sections/{sid}/lessons/{lid}    ← UpdateLessonRequest
DELETE /api/courses/{id}/sections/{sid}/lessons/{lid}
```

## Key flows

**Publish (ADR-005):**
1. Validate: at least 1 published lesson exists
2. Increment `Course.Version`
3. Serialize current structure → `CourseSnapshot.StructureJson`
4. Set `Status = Published`, `PublishedAt = now`
5. Publish `CoursePublished` event

**Enrollment pinning:** When EnrollmentService creates enrollment, it reads
`Course.Version` and stores it as `Enrollment.SnapshotVersion`.
Enrolled students always see the snapshot structure, not the live draft.

## Events published
- `CoursePublished` — on publish call

## Events consumed
- `ContentProcessingCompleted` — update `CourseLesson.DurationSeconds`
- `UserEnrolled` — increment `Course.EnrollmentCount`

## Entities
See `docs/entities.md` → CourseService section.
