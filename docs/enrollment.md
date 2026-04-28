# EnrollmentService

**Port:** 5104 | **DB:** `lms_enrollment` (PostgreSQL) | **ADRs:** ADR-005, ADR-006

## Responsibility
Course enrollment lifecycle: create, check, waitlist, revoke.
Phase 1: free courses only. Paid courses return 409 PAYMENT_REQUIRED (stub).

## Endpoints

```
POST /api/enrollments                      ← EnrollRequest
GET  /api/enrollments/me                   → EnrollmentSummary[]
GET  /api/enrollments/{id}                 → Enrollment
DELETE /api/enrollments/{id}                                      # admin only
GET  /api/enrollments/check                → {enrolled, status}   # internal, NOT via gateway
POST /api/enrollments/manual               ← ManualEnrollRequest  # instructor/admin
GET  /api/enrollments/{courseId}/students  → EnrolledStudent[]    # instructor/admin
POST /api/enrollments/{courseId}/waitlist
```

## Key flows

**Enroll:**
1. Check already enrolled → 409 ALREADY_ENROLLED
2. Check prerequisites met (call CourseService) → 409 PREREQUISITE_NOT_MET
3. Check `IsFree=false` → 409 PAYMENT_REQUIRED (Phase 1 stub)
4. Check seat limit → 409 COURSE_FULL
5. Create `Enrollment {Status=Active, SnapshotVersion=Course.Version}`
6. Publish `UserEnrolled`

**Check endpoint** (called by ContentService, ProgressService):
- Never exposed through YARP gateway
- Returns `{enrolled: bool, status, enrolledAt}`
- Must be fast — called on every video stream request

## Events published
- `UserEnrolled`
- `EnrollmentSuspended`

## Events consumed
- `PaymentProcessed` (Phase 2) — activate pending enrollment
- `GdprErasureRequested` — suspend + anonymise enrollment record

## Entities
See `docs/entities.md` → EnrollmentService section.
