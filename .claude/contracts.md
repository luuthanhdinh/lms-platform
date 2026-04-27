# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.CourseService** (Phase 1, service #3)
Port: **5102** · Database: `lms_courses` (PostgreSQL) · Schema: `courses`
ADRs: ADR-005 (snapshot/version pinning), ADR-006 (IsFree gate)

---

## 1. EF Core entities (LMS.CourseService.Domain)

All inherit `TenantEntity` (`Id: Guid`, `TenantId: Guid`, `CreatedAt: DateTimeOffset`, `UpdatedAt: DateTimeOffset`).
Schema name: `courses`.

```csharp
public class Course : TenantEntity
{
    public Guid InstructorId { get; set; }
    public string Title { get; set; } = default!;          // <= 200
    public string Description { get; set; } = default!;    // <= 4000
    public Guid? ThumbnailContentId { get; set; }
    public string Category { get; set; } = default!;       // <= 80
    public string[] Tags { get; set; } = [];               // text[]
    public DifficultyLevel Difficulty { get; set; }
    public string Language { get; set; } = "vi";           // ISO-639-1
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public bool IsFree { get; set; } = true;               // ADR-006
    public int Version { get; set; } = 1;                  // ADR-005
    public int EnrollmentCount { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public ICollection<CourseSection> Sections { get; set; } = [];
    public ICollection<CoursePrerequisite> Prerequisites { get; set; } = [];
}

public class CourseSection : TenantEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = default!;
    public string Title { get; set; } = default!;          // <= 200
    public int Order { get; set; }
    public ICollection<CourseLesson> Lessons { get; set; } = [];
}

public class CourseLesson : TenantEntity
{
    public Guid SectionId { get; set; }
    public CourseSection Section { get; set; } = default!;
    public Guid CourseId { get; set; }                     // denormalised for query speed
    public string Title { get; set; } = default!;          // <= 200
    public Guid ContentItemId { get; set; }
    public int? DurationSeconds { get; set; }              // updated via ContentProcessingCompleted
    public bool IsFreePreview { get; set; }
    public bool IsOptional { get; set; }
    public int Order { get; set; }
}

public class CoursePrerequisite : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
}

public class CourseSnapshot : TenantEntity
{
    public Guid CourseId { get; set; }
    public int Version { get; set; }
    public string StructureJson { get; set; } = default!;  // jsonb
    public DateTimeOffset SnapshotAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum CourseStatus { Draft, Published, Archived }
public enum DifficultyLevel { Beginner, Intermediate, Advanced }
```

### Indexes
- `Course`: `(TenantId, Status)`, `(TenantId, InstructorId)`, `(TenantId, Category)`, GIN on `Tags`
- `CourseSection`: unique `(CourseId, Order)`
- `CourseLesson`: unique `(SectionId, Order)`, index `(CourseId)`, index `(ContentItemId)`
- `CoursePrerequisite`: unique `(CourseId, PrerequisiteCourseId)`
- `CourseSnapshot`: unique `(CourseId, Version)`

### Global filter
`OnModelCreating` applies `e => e.TenantId == CurrentTenantId` to every `TenantEntity` descendant
(replicate `IdentityDbContext` pattern verbatim).

### Outbox
`AddEntityFrameworkOutbox<CourseDbContext>` — outbox tables created via the same migration.

### Migration filename
`20260427_001_InitialCourseSchema.cs` (initial migration covers all entities + outbox).

---

## 2. Events — `LMS.Contracts/Course/`

### Published

```csharp
public sealed record CoursePublished(
    Guid EventId,
    Guid TenantId,
    Guid CourseId,
    Guid InstructorId,
    int Version,
    DateTimeOffset OccurredAt);
```

NOTE: `docs/events.md` currently shows `CoursePublished` without `EventId`. We **align with the
absolute rule** "every event has EventId, TenantId, OccurredAt" — `EventId` is added.
`docs/events.md` is updated in T11.

### Consumed (no new contracts authored — these already exist or will when their owners ship)

- `LMS.Contracts.Content.ContentProcessingCompleted(Guid ContentItemId, Guid TenantId, string HlsManifestUrl, int DurationSeconds, DateTimeOffset OccurredAt)`
  → updates `CourseLesson.DurationSeconds` for every lesson with matching `ContentItemId` in tenant.
- `LMS.Contracts.Identity.UserEnrolled(Guid UserId, Guid CourseId, Guid TenantId, string PlanType, DateTimeOffset OccurredAt)`
  → atomic increment of `Course.EnrollmentCount`.

Both consumers idempotent. `UserEnrolled` idempotency uses inbox table keyed on
`(UserId, CourseId)` natural composite since the event lacks `EventId`.

---

## 3. HTTP endpoints (LMS.CourseService.Api)

All routes mounted under `/api/courses` and reachable through gateway YARP route
(`/api/courses/**` → `courses` cluster, wired in T7).

Headers (forwarded by gateway, never re-validated):
`X-User-Id`, `X-Tenant-Id`, `X-Roles`.

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| GET | `/api/courses` | public | query: `category?, tag?, difficulty?, page=1, pageSize=20` | 200 `Page<CourseSummary>` | 400 |
| POST | `/api/courses` | role: `instructor`/`admin` | `CreateCourseRequest` | 201 `CourseDetail` | 400, 401, 403 |
| GET | `/api/courses/{id}` | public | — | 200 `CourseDetail` | 404 |
| PUT | `/api/courses/{id}` | owner instructor or admin | `UpdateCourseRequest` | 200 `CourseDetail` | 400, 403, 404, 409 |
| POST | `/api/courses/{id}/publish` | owner instructor | — | 200 `CourseDetail` | 403, 404, 409 (no published lesson), 409 `PAYMENT_REQUIRED` |
| POST | `/api/courses/{id}/unpublish` | owner instructor or admin | — | 200 `CourseDetail` | 403, 404 |
| POST | `/api/courses/{id}/duplicate` | owner instructor or admin | — | 201 `CourseDetail` | 403, 404 |
| GET | `/api/courses/{id}/syllabus` | public | — | 200 `CourseSyllabus` | 404 |
| POST | `/api/courses/{id}/sections` | owner instructor | `SectionRequest` | 201 `SectionDto` | 400, 403, 404 |
| PUT | `/api/courses/{id}/sections/{sid}` | owner instructor | `SectionRequest` | 200 `SectionDto` | 400, 403, 404 |
| DELETE | `/api/courses/{id}/sections/{sid}` | owner instructor | — | 204 | 403, 404 |
| POST | `/api/courses/{id}/sections/{sid}/lessons` | owner instructor | `CreateLessonRequest` | 201 `LessonDto` | 400, 403, 404 |
| PUT | `/api/courses/{id}/sections/{sid}/lessons/{lid}` | owner instructor | `UpdateLessonRequest` | 200 `LessonDto` | 400, 403, 404 |
| DELETE | `/api/courses/{id}/sections/{sid}/lessons/{lid}` | owner instructor | — | 204 | 403, 404 |

### DTO shapes
```csharp
public record CreateCourseRequest(string Title, string Description, string Category,
    string[] Tags, DifficultyLevel Difficulty, string Language, bool IsFree, Guid? ThumbnailContentId);
public record UpdateCourseRequest(string Title, string Description, string Category,
    string[] Tags, DifficultyLevel Difficulty, string Language, bool IsFree, Guid? ThumbnailContentId);
public record SectionRequest(string Title, int Order);
public record CreateLessonRequest(string Title, Guid ContentItemId, int Order, bool IsFreePreview, bool IsOptional);
public record UpdateLessonRequest(string Title, Guid ContentItemId, int Order, bool IsFreePreview, bool IsOptional);

public record CourseSummary(Guid Id, string Title, string Category, string[] Tags,
    DifficultyLevel Difficulty, string Language, CourseStatus Status, bool IsFree,
    int Version, int EnrollmentCount, Guid InstructorId, DateTimeOffset? PublishedAt);
public record CourseDetail(/* CourseSummary fields + */ string Description, Guid? ThumbnailContentId,
    IReadOnlyList<SectionDto> Sections, IReadOnlyList<Guid> Prerequisites);
public record SectionDto(Guid Id, string Title, int Order, IReadOnlyList<LessonDto> Lessons);
public record LessonDto(Guid Id, string Title, Guid ContentItemId, int? DurationSeconds,
    int Order, bool IsFreePreview, bool IsOptional);
public record CourseSyllabus(Guid CourseId, int Version, IReadOnlyList<SyllabusSection> Sections);
public record SyllabusSection(Guid Id, string Title, int Order, IReadOnlyList<SyllabusLesson> Lessons);
public record SyllabusLesson(Guid Id, string Title, int Order, int? DurationSeconds, bool IsFreePreview);
public record Page<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
```

### Error shape
RFC7807 `ProblemDetails`. For payment gate:
```
HTTP/1.1 409 Conflict
Content-Type: application/problem+json
{ "type": "https://lms/errors/payment-required",
  "title": "Payment required",
  "status": 409,
  "code": "PAYMENT_REQUIRED",
  "courseId": "<guid>" }
```

---

## 4. Payment gate rule (ADR-006)

- `POST /api/courses/{id}/publish`: if `Course.IsFree == false` → return **409 PAYMENT_REQUIRED**
  (stub until Phase 2 BillingService exists). The course remains in `Draft`. No event published.
- All other writes ignore `IsFree` for now.

---

## 5. Publish flow (ADR-005, locked)

1. Load `Course` with sections+lessons, validate ownership and `Status == Draft`.
2. Reject with `409 NoPublishedLesson` if course has 0 lessons.
3. If `!IsFree` → `409 PAYMENT_REQUIRED` (no state change).
4. Begin transaction:
   - `Course.Version += 1`
   - Serialize (`Sections + Lessons` projection) → `StructureJson`
   - Insert `CourseSnapshot { CourseId, Version=Course.Version, StructureJson, SnapshotAt=now }`
   - `Course.Status = Published`, `Course.PublishedAt = now`
   - Outbox-publish `CoursePublished(EventId, TenantId, CourseId, InstructorId, Version, OccurredAt)`
5. Commit. MassTransit EF outbox delivers reliably to RabbitMQ.

---

## 6. Domain abstractions to copy (do NOT import from IdentityService)

- `ITenantContext` + `HeaderTenantContext` — replicate from
  `src/services/LMS.IdentityService/LMS.IdentityService.Domain/Abstractions/ITenantContext.cs`
- `AuthorizationHelpers` — replicate (reads `X-Roles` from `HttpContext.Items`/middleware).
- `CourseDbContext` — mirrors `IdentityDbContext` global-filter + `SaveChangesAsync` pattern.

Repositories (interfaces in Domain, EF impls in Infrastructure):
- `ICourseRepository`, `ISectionRepository`, `ILessonRepository`, `ICourseSnapshotRepository`.
All read methods accept `tenantId` explicitly as a defence-in-depth predicate.

---

## 7. Aspire AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`:

```csharp
var courseDb = postgres.AddDatabase("lms-courses");

var courseMigrator = builder.AddProject<Projects.LMS_CourseService_Migrator>("course-migrator")
    .WithReference(courseDb)
    .WaitFor(courseDb);

var course = builder.AddProject<Projects.LMS_CourseService_Api>("courses")
    .WithReference(courseDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(courseMigrator);

gateway.WithReference(course);
```

YARP route for `/api/courses/**` → `courses` cluster. (Gateway transforms inject
`X-User-Id` / `X-Tenant-Id` / `X-Roles`.)

---

## 8. Project layout (locked)

```
src/services/LMS.CourseService/
  LMS.CourseService.Domain/          # entities, enums, ITenantContext, repo interfaces, DTO records
  LMS.CourseService.Infrastructure/  # CourseDbContext, EF configs, repo impls, MassTransit outbox + consumers
  LMS.CourseService.Api/             # Minimal API endpoints, validators, Program.cs, HeaderTenantContext, AuthorizationHelpers
  LMS.CourseService.Migrator/        # IHostedService that runs migrations on start
```

---

## 9. Tests (locked surface)

- Unit: publish flow rules, payment gate, snapshot serialization, validators.
- Integration (`LMS.IntegrationTests`):
  - Tenant isolation: cannot read/write across tenants.
  - Payment gate: publish IsFree=false → 409 PAYMENT_REQUIRED.
  - Publish happy path emits `CoursePublished` (MassTransit test harness).
  - Consumer: `ContentProcessingCompleted` updates lesson duration.
  - Consumer: `UserEnrolled` increments EnrollmentCount idempotently.
- Architecture: every entity inherits `TenantEntity`; DbContext has global filter.
- Contract: `CoursePublished` shape stable.

---

## 10. Decisions (locked — approved by product owner 2026-04-27)

1. **`CourseArchived` event** — `POST /{id}/unpublish` publishes `CourseArchived` via outbox.
   Record (add to `LMS.Contracts/Course/CourseArchived.cs`):
   ```csharp
   public sealed record CourseArchived(
       Guid EventId, Guid TenantId, Guid CourseId,
       Guid InstructorId, DateTimeOffset OccurredAt);
   ```
   EnrollmentService will consume this in Phase 1 to suspend active enrollments.

2. **Public endpoint tenant scope** — Phase 1: read `X-Tenant-Id` header only (gateway
   always forwards it, even for unauthenticated requests). Slug-based resolution is Phase 2.
   If `X-Tenant-Id` is missing/invalid on public GETs → `400 TENANT_REQUIRED`.

3. **Owner check** — "owner-or-admin" pattern:
   - `Course.InstructorId == ctx.UserId` → allowed (any matching instructor)
   - role is `admin` or `org-admin` → allowed (bypass ownership)
   - Otherwise → `403 FORBIDDEN`
   Apply this to: `PUT /{id}`, `POST /{id}/publish`, `POST /{id}/unpublish`,
   `POST /{id}/duplicate`, all section and lesson write endpoints.
