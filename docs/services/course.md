# LMS.CourseService

**Responsibility:** Manages course catalog, sections, lessons, and publishes snapshots. Enforces instructor ownership and payment gates. Consumes ContentService events (lesson durations) and EnrollmentService events (enrollment counts).

**Port / AppHost:** `courses` / port 5102 | **Database:** PostgreSQL `lms_courses`, schema `courses`

**Architecture decisions:** [ADR-005 (snapshot/version pinning)](../adr/adr-005-course-versioning.md), [ADR-006 (IsFree payment gate)](../adr/adr-006-payment-gate.md)

---

## Entities

All inherit `TenantEntity`. Schema: `courses`.

| Entity | Key fields | Notes |
|---|---|---|
| `Course` | InstructorId, Title, Description, Category, Tags[], Difficulty, Language, Status, IsFree, Version, EnrollmentCount, PublishedAt | Indexed on `(TenantId, Status)`, `(TenantId, InstructorId)`, `(TenantId, Category)` |
| `CourseSection` | CourseId, Title, Order | Unique `(CourseId, Order)` |
| `CourseLesson` | SectionId, CourseId, Title, ContentItemId, DurationSeconds, IsFreePreview, IsOptional, Order | DurationSeconds updated via ContentProcessingCompleted |
| `CoursePrerequisite` | CourseId, PrerequisiteCourseId | Unique constraint |
| `CourseSnapshot` | CourseId, Version, StructureJson (jsonb), SnapshotAt | Created on publish; unique `(CourseId, Version)` |

**Enums:** `CourseStatus` (Draft|Published|Archived), `DifficultyLevel` (Beginner|Intermediate|Advanced)

---

## HTTP Endpoints

All routes under `/api/courses`. Headers forwarded by gateway: `X-User-Id`, `X-Tenant-Id`, `X-Roles`.

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/courses` | public | List courses (filters: category, tag, difficulty, page, pageSize) |
| `POST` | `/api/courses` | instructor/admin | Create draft course |
| `GET` | `/api/courses/{id}` | public | Get course detail |
| `PUT` | `/api/courses/{id}` | owner/admin | Update metadata |
| `POST` | `/api/courses/{id}/publish` | owner | Snapshot, version++, emit CoursePublished; **409 if IsFree=false** |
| `POST` | `/api/courses/{id}/unpublish` | owner/admin | Archive, emit CourseArchived |
| `POST` | `/api/courses/{id}/duplicate` | owner/admin | Clone to new Draft course |
| `GET` | `/api/courses/{id}/syllabus` | public | Public-safe structure (no edit fields) |
| `POST` | `/api/courses/{id}/sections` | owner | Create section |
| `PUT` | `/api/courses/{id}/sections/{sid}` | owner | Update section |
| `DELETE` | `/api/courses/{id}/sections/{sid}` | owner | Delete section & lessons |
| `POST` | `/api/courses/{id}/sections/{sid}/lessons` | owner | Create lesson |
| `PUT` | `/api/courses/{id}/sections/{sid}/lessons/{lid}` | owner | Update lesson |
| `DELETE` | `/api/courses/{id}/sections/{sid}/lessons/{lid}` | owner | Delete lesson |

**Owner-or-admin rule:** `Course.InstructorId == X-User-Id` OR role in [`admin`, `org-admin`].

**Error codes:** 400 (INVALID_REQUEST, TENANT_REQUIRED), 403 (FORBIDDEN), 404 (NOT_FOUND), 409 (PAYMENT_REQUIRED, NO_PUBLISHED_LESSON)

---

## Events

### Published (via EF Core outbox)

```csharp
public record CoursePublished(Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, int Version, DateTimeOffset OccurredAt);

public record CourseArchived(Guid EventId, Guid TenantId, Guid CourseId,
    Guid InstructorId, DateTimeOffset OccurredAt);
```

### Consumed

- **`ContentProcessingCompleted`** (ContentService) → updates `CourseLesson.DurationSeconds` for matching ContentItemId
- **`UserEnrolled`** (EnrollmentService) → increments `Course.EnrollmentCount` (idempotent via inbox table)

---

## Publish Flow (ADR-005)

1. Load Course; validate `Status == Draft` and ownership
2. Reject `409 NO_PUBLISHED_LESSON` if 0 lessons exist
3. If `IsFree == false` → reject `409 PAYMENT_REQUIRED` (no state change, no event)
4. Transaction:
   - Increment `Course.Version`
   - Insert `CourseSnapshot` with serialized structure
   - Set `Status = Published`, `PublishedAt = now`
   - Outbox-publish `CoursePublished`
5. Commit. MassTransit delivers reliably to RabbitMQ

---

## Payment Gate (ADR-006)

- Publish endpoint: if `IsFree == false` → **409 PAYMENT_REQUIRED** stub (no state change)
- Phase 2: BillingService will unblock

---

## Project Layout

```
LMS.CourseService/
  Domain/          # Entities, enums, interfaces, DTOs
  Infrastructure/  # DbContext, EF configs, repos, MassTransit consumers, outbox
  Api/             # Minimal APIs, validators, HeaderTenantContext, AuthorizationHelpers
  Migrator/        # IHostedService for EF Core migrations on startup
```

---

## AppHost Wiring

```csharp
var courseDb = postgres.AddDatabase("lms-courses");
var courseMigrator = builder.AddProject<LMS_CourseService_Migrator>("course-migrator")
    .WithReference(courseDb).WaitFor(courseDb);
var course = builder.AddProject<LMS_CourseService_Api>("courses")
    .WithReference(courseDb).WithReference(rabbitmq).WaitForCompletion(courseMigrator);
gateway.WithReference(course);
```

YARP routes `/api/courses/**` → `courses` cluster. Gateway transforms inject trusted headers.
