# LMS.EnrollmentService

**Responsibility:** Manages learner enrollment in courses. Validates IsFree gate, publishes enrollment events, and consumes course lifecycle events (archive).

**Port / AppHost:** `enrollment` / port 5104 | **Database:** PostgreSQL `lms_enrollments`, schema `enrollments`

**Architecture decisions:** ADR-006 (IsFree payment gate), multi-tenant isolation via EF Core query filter.

---

## Entities

All inherit `TenantEntity`. Schema: `enrollments`.

| Entity | Key fields | Notes |
|---|---|---|
| `Enrollment` | UserId, CourseId, Status, IsFree, EnrolledAt, CompletedAt, CancelledAt, SuspendedAt, SuspensionReason | Unique partial index `(TenantId, UserId, CourseId)` WHERE `Status = Active`; indexes on `(TenantId, UserId, Status)` and `(TenantId, CourseId, Status)` for list/count queries. Concurrency token: `xmin` (Postgres row version). |
| `WaitlistEntry` | UserId, CourseId, Position, JoinedAt | Tracks queue for capacity-limited courses (Phase 2+). |

**Enums:** `EnrollmentStatus` (Active|Completed|Suspended|Cancelled)

---

## HTTP Endpoints

All routes under `/api/enrollments`. Headers forwarded by gateway: `X-User-Id`, `X-Tenant-Id`, `X-Roles`.

| Method | Path | Auth | Request | 2xx | 4xx/5xx |
|---|---|---|---|---|---|
| `POST` | `/api/enrollments` | student \| admin | `{ courseId: guid, isFree: bool }` | 201 `EnrollmentDto` | 400 TENANT_REQUIRED, 401 UNAUTHORIZED, 403 FORBIDDEN, 409 PAYMENT_REQUIRED, 409 ALREADY_ENROLLED |
| `GET` | `/api/enrollments` | authenticated | query: `status?` (Active\|Completed\|Suspended\|Cancelled), `page=1, pageSize=20` | 200 `Page<EnrollmentDto>` | 400 TENANT_REQUIRED, 401 UNAUTHORIZED |
| `GET` | `/api/enrollments/{id}` | authenticated (owner \| admin) | — | 200 `EnrollmentDto` | 400 TENANT_REQUIRED, 401 UNAUTHORIZED, 403 FORBIDDEN, 404 ENROLLMENT_NOT_FOUND |
| `DELETE` | `/api/enrollments/{id}` | authenticated (owner \| admin) | — | 204 | 400 TENANT_REQUIRED, 401 UNAUTHORIZED, 403 FORBIDDEN, 404 ENROLLMENT_NOT_FOUND, 409 NOT_ACTIVE |
| `GET` | `/api/enrollments/courses/{courseId}` | instructor \| admin | query: `status?`, `page=1, pageSize=20` | 200 `Page<EnrollmentDto>` | 400 TENANT_REQUIRED, 401 UNAUTHORIZED, 403 FORBIDDEN |
| `GET` | `/api/enrollments/courses/{courseId}/count` | instructor \| admin | — | 200 `{ courseId: guid, activeCount: int, totalCount: int }` | 400 TENANT_REQUIRED, 401 UNAUTHORIZED, 403 FORBIDDEN |

**Authorization rules:**
- `POST /api/enrollments`: caller role = `student` OR (`admin`/`org-admin` for enrol-on-behalf). `UserId` on inserted row comes from `X-User-Id` header.
- `GET /api/enrollments`: returns caller's own enrollments unless role = `admin` (returns all tenant enrollments).
- `GET /api/enrollments/{id}`: owner can read own; `admin` can read any.
- `DELETE /api/enrollments/{id}`: owner can cancel own active enrollment; `admin` can cancel any.
- Course-scoped list/count: require `instructor` (of that course) or `admin`.

**IsFree gate (Phase 1 stub):**
- `POST /api/enrollments` body includes `isFree: bool` (sourced from course catalog).
- If `isFree == false` → return `409 PAYMENT_REQUIRED` with no state change.
- Phase 2: BillingService will unblock; EnrollmentService remains HTTP-isolated per absolute rules.

**Re-enrollment:**
- If active enrollment exists for `(TenantId, UserId, CourseId)` → `409 ALREADY_ENROLLED`.
- If enrollment exists in `Cancelled`, `Suspended`, or `Completed` state → allow new `Active` enrollment.

---

## Events

### Published (via EF Core outbox)

```csharp
public record UserEnrolled(
    Guid UserId, Guid CourseId, Guid TenantId,
    string PlanType, DateTimeOffset OccurredAt);
```
- **Published by:** `POST /api/enrollments` endpoint on successful insert.
- **Consumers:** ProgressService (seed progress record), CourseService (increment `Course.EnrollmentCount`), NotificationWorker (welcome email).
- **Idempotency:** No `EventId` — consumers dedupe on natural key `(UserId, CourseId)`.

```csharp
public record EnrollmentCancelled(
    Guid EventId, Guid TenantId, Guid EnrollmentId,
    Guid UserId, Guid CourseId, DateTimeOffset OccurredAt);
```
- **Published by:** `DELETE /api/enrollments/{id}` endpoint on successful cancellation.
- **Consumers:** ProgressService (freeze progress), CourseService (decrement `Course.EnrollmentCount`), NotificationWorker (cancellation email).
- **Idempotency:** Carries `EventId` (cancellation is not naturally idempotent on `(UserId, CourseId)` — user can re-enroll).
- **Phase 1 note:** Event published for forward-compatibility; CourseService/ProgressService consumers implemented in Phase 2.

### Consumed

**`CourseArchived`** (CourseService)
- Behavior: Load all `Enrollment` rows for `(TenantId, CourseId)` with `Status = Active`, set `Status = Suspended`, `SuspendedAt = now`, `SuspensionReason = "course-archived"`. Idempotent on `EventId` via MassTransit inbox. Single `SaveChanges` transaction.

---

## Project Layout

```
LMS.EnrollmentService/
  Domain/             # Entities, enums, repository interfaces, ITenantContext, DTO records
  Infrastructure/     # EnrollmentDbContext, repo implementations, CourseArchivedConsumer, DI extensions
  Api/                # Minimal API endpoints, validators, AuthorizationHelpers, Program.cs, header tenancy context
  Migrator/           # IHostedService running EF Core migrations on startup
```

---

## AppHost Wiring

```csharp
var enrollmentDb = postgres.AddDatabase("lms-enrollments");
var enrollmentMigrator = builder.AddProject<LMS_EnrollmentService_Migrator>("enrollment-migrator")
    .WithReference(enrollmentDb).WaitFor(enrollmentDb);
var enrollment = builder.AddProject<LMS_EnrollmentService_Api>("enrollment")
    .WithReference(enrollmentDb).WithReference(rabbitmq).WaitForCompletion(enrollmentMigrator);
gateway.WithReference(enrollment);
```

YARP routes `/api/enrollments/**` → `enrollment` cluster. Gateway transforms inject trusted headers.
