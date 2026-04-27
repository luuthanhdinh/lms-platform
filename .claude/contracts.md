# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.EnrollmentService** (Phase 1, service #5)
Port: **5104** · Database: PostgreSQL `lms-enrollments` · Schema: `enrollments`
ADRs: tenant-isolation (EF global filter), event-driven cross-service comms,
IsFree gate stub (matches CourseService Phase 1 pattern).

> EnrollmentService follows the standard 4-project layout
> (Domain / Infrastructure / Api / Migrator) — Migrator slot is back since
> the service uses PostgreSQL via EF Core.

---

## 1. Project layout (locked — 4 projects)

```
src/services/LMS.EnrollmentService/
  LMS.EnrollmentService.Domain/          # entities, enums, repository + ITenantContext, DTO records
  LMS.EnrollmentService.Infrastructure/  # EnrollmentDbContext, repos, MassTransit consumers, DI extensions
  LMS.EnrollmentService.Api/             # Minimal API endpoints, validators, Program.cs, header tenancy
  LMS.EnrollmentService.Migrator/        # IHostedService running EF Core migrations once on startup
```

References:
- Domain → `LMS.SharedKernel`
- Infrastructure → Domain, `LMS.Contracts`, `LMS.ServiceDefaults`
- Api → Infrastructure, `LMS.ServiceDefaults`
- Migrator → Infrastructure, `LMS.ServiceDefaults`

NuGet packages (Infrastructure): `Microsoft.EntityFrameworkCore`,
`Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit`,
`MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`.

---

## 2. Entity (LMS.EnrollmentService.Domain)

`Enrollment : TenantEntity` (inherits `Id`, `TenantId`, `CreatedAt`, `UpdatedAt`).

```csharp
public class Enrollment : TenantEntity
{
    public Guid UserId { get; set; }              // student
    public Guid CourseId { get; set; }
    public EnrollmentStatus Status { get; set; }  // Active, Completed, Suspended, Cancelled
    public bool IsFree { get; set; }              // denormalised from course at enrol time (Phase 1 stub)
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; } // e.g. "course-archived"
}

public enum EnrollmentStatus { Active = 0, Completed = 1, Suspended = 2, Cancelled = 3 }
```

### EF Core configuration (in `EnrollmentDbContext.OnModelCreating`)
- Schema: `enrollments`. Table: `enrollments`.
- Global query filter: `b.HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`
  on `Enrollment` (matches CourseService pattern).
- Indexes:
  - Unique `(TenantId, UserId, CourseId)` filtered `WHERE Status IN (Active)`
    (Postgres partial unique index — prevents duplicate active enrolment).
  - `(TenantId, UserId, Status)` for "list my enrolments".
  - `(TenantId, CourseId, Status)` for "course enrolment count / list".
- Concurrency token: `xmin` (Postgres `[Timestamp]` column via
  `.IsRowVersion()` mapping to `xmin`).

### Migration
- Filename: `20260427_001_InitialEnrollmentSchema`
- Creates schema `enrollments`, table `enrollments` with the columns and
  indexes above.

---

## 3. Events — `LMS.Contracts/Enrollment/`

### Already exists (do NOT modify — verified 2026-04-27)
`src/LMS.Contracts/Enrollment/UserEnrolled.cs`:
```csharp
public sealed record UserEnrolled(
    Guid UserId, Guid CourseId, Guid TenantId,
    string PlanType, DateTimeOffset OccurredAt);
```
Note: intentionally NO `EventId`. Consumers (CourseService, ProgressService,
NotificationWorker) dedupe on the `(UserId, CourseId)` natural key.
**Do not add `EventId`.** This decision is locked per CourseService contracts.

### New record to add (T1)
```csharp
/// <summary>
/// Published by EnrollmentService when an enrolment is cancelled (by the learner
/// or by an admin). Consumers: ProgressService (freeze progress), CourseService
/// (decrement enrolment count), NotificationWorker (cancellation email).
/// </summary>
public sealed record EnrollmentCancelled(
    Guid EventId,
    Guid TenantId,
    Guid EnrollmentId,
    Guid UserId,
    Guid CourseId,
    DateTimeOffset OccurredAt);
```

> Carries `EventId` because cancellation is not naturally idempotent on
> `(UserId, CourseId)` — a user could re-enrol after cancelling.
> No CourseService/ProgressService consumer is required in this sprint;
> the event is emitted now for forward-compatibility.

### Consumed events
- `LMS.Contracts.Course.CourseArchived` — handled in Infrastructure layer.
  Behaviour: load all `Enrollment` rows for `(TenantId, CourseId)` with
  `Status = Active`, set `Status = Suspended`, `SuspendedAt = now`,
  `SuspensionReason = "course-archived"`. Idempotent on `EventId` via
  MassTransit inbox. Save in single SaveChanges.
- `LMS.Contracts.Course.CoursePublished` — NOT consumed (informational only).

### Publish flow
- Api `POST /api/enrollments` → on successful insert, publish
  `UserEnrolled` via MassTransit transactional outbox
  (`AddEntityFrameworkOutbox<EnrollmentDbContext>`).
- Api `DELETE /api/enrollments/{id}` → on cancel, publish
  `EnrollmentCancelled` via outbox.

---

## 4. MassTransit + outbox

- EF Core transactional outbox via `AddEntityFrameworkOutbox<EnrollmentDbContext>`
  (creates `OutboxMessage`, `OutboxState`, `InboxState` tables in
  `enrollments` schema — generated by EF migration).
- Inbox: `CourseArchivedConsumer` is automatically idempotent on `EventId`.
- Retry policy: `UseMessageRetry(r => r.Intervals(1s, 5s, 30s))` then poison.

---

## 5. HTTP endpoints (LMS.EnrollmentService.Api)

All routes mounted under `/api/enrollments`. Gateway YARP route
`/api/enrollments/{**rest}` → `enrollment` cluster **already configured** in
`src/gateway/LMS.Gateway/appsettings.json` (verified). Cluster destination
will resolve via Aspire service discovery once AppHost wires the project as
`enrollment`.

Headers (forwarded by gateway, never re-validated):
`X-User-Id`, `X-Tenant-Id`, `X-Roles`. Missing tenant header → `400 TENANT_REQUIRED`.

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| POST | `/api/enrollments` | role: `student` OR `admin`/`org-admin` (admin-on-behalf bypass) | `EnrollRequest` | 201 `EnrollmentDto` | 400, 401, 403, 409 PAYMENT_REQUIRED, 409 ALREADY_ENROLLED |
| GET | `/api/enrollments` | tenant scope | query: `status?, page=1, pageSize=20` | 200 `Page<EnrollmentDto>` | 400 |
| GET | `/api/enrollments/{id}` | tenant scope | — | 200 `EnrollmentDto` | 404 |
| DELETE | `/api/enrollments/{id}` | owner or admin | — | 204 | 403, 404, 409 NOT_ACTIVE |
| GET | `/api/enrollments/courses/{courseId}` | role: `instructor` or `admin` | query: `status?, page=1, pageSize=20` | 200 `Page<EnrollmentDto>` | 400 |
| GET | `/api/enrollments/courses/{courseId}/count` | role: `instructor` or `admin` | — | 200 `EnrollmentCountDto` | — |

### Authorization rules
- `GET /api/enrollments` returns the caller's own enrolments unless caller
  has role `admin`, in which case it returns all enrolments for the tenant.
- `GET /api/enrollments/{id}` — owner can read own; `instructor` of the
  course OR `admin` can read any. Cross-tenant lookups → `404` (leak-safe).
- `POST /api/enrollments` — caller MUST have role `student`. `admin` or
  `org-admin` may also call this endpoint (enrol-on-behalf bypass). Any other
  role → `403`. The `UserId` on the inserted row is taken from the
  `X-User-Id` header (admin-on-behalf flows pass the target student's id via
  that header — body never carries `UserId`).
- `DELETE /api/enrollments/{id}` — owner can cancel own active enrolment;
  `admin` can cancel any. Cancelling a non-active enrolment → `409 NOT_ACTIVE`.
- Course-scoped list endpoints require `instructor` (of that course) or `admin`.

### IsFree gate (Phase 1 stub)
`POST /api/enrollments` body MUST include `IsFree: bool`. The frontend
populates this from the course catalog DTO. If `IsFree == false`, return
`409 PAYMENT_REQUIRED` (matches CourseService publish gate). The flag is
persisted on the row for audit. **Phase 2** replaces this with a billing
check — EnrollmentService still does not call CourseService directly
(absolute rule).

### Duplicate gate
If an `Enrollment` already exists for `(TenantId, UserId, CourseId)` with
`Status = Active` → `409 ALREADY_ENROLLED`. If exists in `Cancelled`,
`Suspended`, or `Completed` state, allow a new `Active` row to be created
(re-enrolment).

### DTOs
```csharp
public record EnrollRequest(Guid CourseId, bool IsFree);

public record EnrollmentDto(
    Guid Id, Guid TenantId, Guid UserId, Guid CourseId,
    EnrollmentStatus Status, bool IsFree,
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
    DateTimeOffset? SuspendedAt, string? SuspensionReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record EnrollmentCountDto(Guid CourseId, int ActiveCount, int TotalCount);

public record Page<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
```

### Error shape
RFC7807 `ProblemDetails`. Error codes (in `extensions.code`):
`PAYMENT_REQUIRED`, `ALREADY_ENROLLED`, `NOT_ACTIVE`, `TENANT_REQUIRED`.

---

## 6. AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`:

```csharp
var enrollmentDb = postgres.AddDatabase("lms-enrollments");

var enrollmentMigrator = builder.AddProject<Projects.LMS_EnrollmentService_Migrator>("enrollment-migrator")
    .WithReference(enrollmentDb)
    .WaitFor(enrollmentDb);

var enrollment = builder.AddProject<Projects.LMS_EnrollmentService_Api>("enrollment")
    .WithReference(enrollmentDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(enrollmentMigrator);

gateway.WithReference(enrollment);
```

Aspire resource name **must** be `enrollment` to match the YARP cluster
destination `http://enrollment`.

Gateway `appsettings.json` already routes `/api/enrollments/{**rest}` →
`enrollment` cluster — no gateway change required.

---

## 7. Configuration keys

```
ConnectionStrings:lms-enrollments    (Aspire-injected Postgres connection string)
ConnectionStrings:rabbitmq           (Aspire-injected)
```

ASPNETCORE_URLS port `5104` set in `launchSettings.json` for local dev;
Aspire overrides at runtime.

---

## 8. Tests (locked surface)

Unit (in Api/Domain test projects):
- `EnrollmentDto` mapping.
- Validators: `EnrollRequest.CourseId` non-empty.
- Authorization helper: owner-or-admin / instructor-or-admin checks.

Integration (`LMS.IntegrationTests/EnrollmentService/`) — Testcontainers
Postgres + RabbitMQ + MassTransit harness:
- Tenant isolation pair: tenant A cannot GET / DELETE tenant B's enrolment (404).
- `POST /api/enrollments` with `IsFree=true` → 201, row persisted, publishes `UserEnrolled`.
- `POST /api/enrollments` with `IsFree=false` → 409 PAYMENT_REQUIRED.
- Duplicate active enrolment → 409 ALREADY_ENROLLED.
- Re-enrol after cancel succeeds.
- `DELETE /api/enrollments/{id}` by owner → 204, status becomes Cancelled,
  publishes `EnrollmentCancelled` (with `EventId`).
- `GET /api/enrollments` returns only caller's enrolments for non-admin.
- `GET /api/enrollments/courses/{courseId}/count` returns active+total.
- `CourseArchived` consumer suspends all active enrolments for the course
  (idempotent on `EventId`).

Architecture (`LMS.ArchitectureTests`):
- `Enrollment` inherits `TenantEntity`.
- `EnrollmentDbContext` applies global query filter on `TenantId` for `Enrollment`.

Contract (`LMS.ContractTests`):
- `UserEnrolled` shape unchanged (no `EventId`).
- `EnrollmentCancelled` shape stable.

---

## 9. Open decisions (auto-resolved — flagged for human review in summary)

1. **`EnrollmentCancelled` event added** with `EventId` (cancellation is not
   naturally idempotent on `(UserId, CourseId)` since users may re-enrol).
   No consumer wired this sprint — emitted for forward-compatibility.
2. **IsFree denormalised** on the `Enrollment` row from the request body.
   Frontend supplies the value from the cached course catalog. Phase 2
   replaces with a billing service callout (still no direct cross-service HTTP).
3. **Re-enrolment after cancel allowed** — partial unique index is filtered on
   `Status = Active` only. Historical rows preserved for audit.
4. **Course-archive suspension is non-destructive** — sets `Status=Suspended`
   with reason `course-archived`. Cancellation remains a separate explicit user action.
5. **Aspire DB resource name `lms-enrollments`** (kebab-case to match other
   Aspire databases like `lms-identity`, `lms-courses`, `lms-content`).
   Overrides the placeholder `lms_enrollment` left in AppHost comments.
