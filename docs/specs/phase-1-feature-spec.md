# LMS Platform — Phase 1 Feature Specification

**Phase:** 1 — Foundation & Core  
**Sprints:** 1–8 (16 weeks)  
**Goal:** A production-ready LMS skeleton where students can register, browse courses, watch content, track progress, and complete basic assessments.  
**Stack:** .NET 9 · Aspire · Keycloak · YARP · PostgreSQL · MongoDB · Redis · RabbitMQ · MassTransit · S3/CDN

---

## Table of Contents

1. [WP 1.1 — Keycloak SSO Setup](#wp-11--keycloak-sso-setup)
2. [WP 1.2 — Aspire AppHost Scaffold](#wp-12--aspire-apphost-scaffold)
3. [WP 1.3 — YARP API Gateway](#wp-13--yarp-api-gateway)
4. [WP 1.4 — Service Defaults & Telemetry](#wp-14--service-defaults--telemetry)
5. [WP 1.5 — Database Provisioning](#wp-15--database-provisioning)
6. [WP 1.6 — Event Bus (RabbitMQ + MassTransit)](#wp-16--event-bus-rabbitmq--masstransit)
7. [WP 1.7 — Feature Flags](#wp-17--feature-flags)
8. [WP 2.1 — Identity Service](#wp-21--identity-service)
9. [WP 2.2 — Course Service](#wp-22--course-service)
10. [WP 2.3 — Content Delivery Service](#wp-23--content-delivery-service)
11. [WP 2.4 — Enrollment Service](#wp-24--enrollment-service)
12. [WP 2.5 — Progress Service (xAPI)](#wp-25--progress-service-xapi)
13. [WP 2.6 — Notification Worker](#wp-26--notification-worker)
14. [WP 3.1 — Assessment Service](#wp-31--assessment-service)
15. [WP 7.5 — Certificate Service (basic)](#wp-75--certificate-service-basic)
16. [Open Design Decisions](#open-design-decisions)
17. [Phase 1 Event Contract Summary](#phase-1-event-contract-summary)
18. [Phase 1 API Surface Summary](#phase-1-api-surface-summary)

---

## WP 1.1 — Keycloak SSO Setup

**Sprint:** 1–2 | **Owner:** DevOps | **Effort:** 2w

### Context

Keycloak is the single identity plane for the entire platform. Every microservice trusts tokens issued by Keycloak. No service implements its own auth logic.

### User stories

**US-1.1.1** — As a student, I can register with my email and password so that I have an account on the platform.

**US-1.1.2** — As a student, I can log in with Google or Microsoft so that I don't need a separate password.

**US-1.1.3** — As a student, I can log in with MFA (TOTP) so that my account is protected.

**US-1.1.4** — As an organisation admin, I can configure my company's SAML/OIDC identity provider so that my employees use SSO to access the platform.

**US-1.1.5** — As a system, all microservices validate JWT tokens against Keycloak's JWKS endpoint so that auth is centralised and stateless.

**US-1.1.6** — As a student, when my access token expires I am silently re-authenticated using the refresh token so that my session is uninterrupted.

### Acceptance criteria

- [ ] A `lms` realm exists in Keycloak with the following clients registered: `lms-gateway`, `lms-admin-portal`, `lms-student-app`, `lms-instructor-app`
- [ ] Realm is exported as `infra/keycloak/realm-export.json` and auto-imported on Aspire container startup
- [ ] The following realm roles exist: `student`, `instructor`, `admin`, `org-admin`
- [ ] Google and Microsoft identity providers are configured as social login options
- [ ] TOTP MFA is available and can be enforced at the realm or client level
- [ ] JWT access tokens contain the following custom claims: `tenant_id`, `user_id`, `realm_access.roles`
- [ ] Access token TTL = 5 minutes; refresh token TTL = 8 hours (configurable per realm)
- [ ] A negative test confirms that a tampered or expired JWT returns 401 from the gateway
- [ ] JWKS endpoint `/.well-known/openid-configuration` is reachable from all services inside the Aspire network
- [ ] `realm-export.json` is committed to the repo and CI imports it cleanly on every run

### Open decisions

- [ ] **OD-1.1.a:** Do we use a single `lms` realm for all tenants (with `tenant_id` claim) or one realm per tenant? → Recommendation: single realm + `tenant_id` claim for Phase 1; per-tenant realms deferred to Phase 3 white-label work (WP 9.5).
- [ ] **OD-1.1.b:** Should MFA be optional or enforced for instructors? → Needs product decision.

---

## WP 1.2 — Aspire AppHost Scaffold

**Sprint:** 1 | **Owner:** Lead Dev | **Effort:** 1w

### Context

The AppHost is the single source of truth for how all services, databases, and infrastructure components are wired together. It runs locally via `dotnet run --project LMS.AppHost` and produces manifests for cloud deployment.

### User stories

**US-1.2.1** — As a developer, I can run the entire platform locally with a single command so that I can develop without manual container management.

**US-1.2.2** — As a developer, I can see all service health, logs, traces, and metrics in the Aspire Dashboard so that I can debug without external tooling.

**US-1.2.3** — As a DevOps engineer, I can run `aspire publish` to generate Docker Compose and ACA/AKS deployment manifests so that the same config drives all environments.

### Acceptance criteria

- [ ] Solution structure matches the agreed layout:
  ```
  LMS.sln
  ├── src/
  │   ├── LMS.AppHost/
  │   ├── LMS.ServiceDefaults/
  │   ├── LMS.Contracts/
  │   ├── LMS.Gateway/
  │   ├── LMS.IdentityService/
  │   ├── LMS.CourseService/
  │   ├── LMS.ContentService/
  │   ├── LMS.EnrollmentService/
  │   ├── LMS.ProgressService/
  │   ├── LMS.AssessmentService/
  │   └── LMS.NotificationWorker/
  └── infra/
      ├── keycloak/realm-export.json
      └── docker-compose.override.yml
  ```
- [ ] `dotnet run --project LMS.AppHost` starts all services, databases, and Keycloak with no manual steps
- [ ] Aspire Dashboard is accessible at `http://localhost:18888` showing all registered resources
- [ ] All service-to-service URLs and connection strings are injected via Aspire `WithReference()` — no hardcoded values anywhere
- [ ] `aspire publish` produces a valid Docker Compose file that runs on a clean machine
- [ ] GitHub Actions CI pipeline: build → unit test → integration test → publish artifact (runs on every PR)

---

## WP 1.3 — YARP API Gateway

**Sprint:** 1–2 | **Owner:** Backend | **Effort:** 1w

### Context

YARP (Yet Another Reverse Proxy) is an ASP.NET Core library that runs inside `LMS.Gateway`. It is the single public entry point. It validates JWTs, enforces rate limiting, and forwards enriched requests to downstream services.

### User stories

**US-1.3.1** — As a client app, all API calls go to a single base URL so that I don't need service discovery logic.

**US-1.3.2** — As a system, the gateway validates the JWT before any downstream service sees the request so that services can trust forwarded identity claims.

**US-1.3.3** — As a system, the gateway forwards `X-User-Id`, `X-Tenant-Id`, and `X-Roles` headers to downstream services so that services don't need to re-parse the JWT.

**US-1.3.4** — As a system, the gateway enforces per-user rate limiting so that individual users cannot overload the backend.

**US-1.3.5** — As a student, public endpoints (course catalog, login redirect) are accessible without a token so that I can browse before signing in.

### Acceptance criteria

- [ ] YARP routes are defined in `appsettings.json` (or code) covering all Phase 1 services
- [ ] JWT validation uses Keycloak's JWKS — no local secret. Validation checks: signature, expiry, issuer, audience
- [ ] On valid JWT: gateway extracts `sub` → `X-User-Id`, `tenant_id` → `X-Tenant-Id`, `realm_access.roles` → `X-Roles` and forwards as headers
- [ ] On invalid/missing JWT for a protected route: gateway returns `401 Unauthorized` with no call made to downstream
- [ ] Public routes (defined in config) bypass JWT check: `GET /api/courses`, `GET /api/courses/{id}`, `/health`
- [ ] Rate limiter: 100 requests/minute per `X-User-Id`; returns `429 Too Many Requests` with `Retry-After` header
- [ ] Gateway has its own `/health` and `/ready` endpoints
- [ ] All gateway access logs include `CorrelationId` (generated if absent), `UserId`, `TenantId`, `RouteId`, `StatusCode`, `DurationMs`
- [ ] A load test (k6) confirms gateway handles 500 concurrent users at < 50ms added latency

### Route table (Phase 1)

| Method | Path pattern | Downstream service | Auth required |
|---|---|---|---|
| `*` | `/api/identity/**` | LMS.IdentityService | Varies |
| `*` | `/api/courses/**` | LMS.CourseService | GET = public |
| `*` | `/api/content/**` | LMS.ContentService | Yes |
| `*` | `/api/enrollments/**` | LMS.EnrollmentService | Yes |
| `*` | `/api/progress/**` | LMS.ProgressService | Yes |
| `*` | `/api/assessments/**` | LMS.AssessmentService | Yes |
| `GET` | `/health` | Gateway self | No |

---

## WP 1.4 — Service Defaults & Telemetry

**Sprint:** 1 | **Owner:** Lead Dev | **Effort:** 1w

### Context

`LMS.ServiceDefaults` is a shared class library referenced by every microservice. It configures OpenTelemetry, health checks, and Polly resilience in one place so that every service gets these for free.

### User stories

**US-1.4.1** — As a developer, every service I build automatically has distributed tracing so that I can follow a request across services in the Aspire Dashboard.

**US-1.4.2** — As a DevOps engineer, every service exposes a `/health` endpoint so that the orchestrator can determine liveness and readiness.

**US-1.4.3** — As a developer, all HTTP client calls from one service to another automatically retry on transient failures so that I don't write retry logic per call.

### Acceptance criteria

- [ ] `AddServiceDefaults()` extension method configures: OpenTelemetry (traces + metrics + logs), health checks, Polly HTTP retry (3 attempts, exponential backoff), service discovery via Aspire
- [ ] Every service registers and exposes `/health` (liveness) and `/ready` (readiness) — readiness checks all downstream dependencies (DB, Redis, bus)
- [ ] All traces include: `service.name`, `service.version`, `tenant_id`, `user_id`, `correlation_id`
- [ ] Traces are visible in Aspire Dashboard with parent-child relationships across service boundaries
- [ ] HTTP client retry policy: 3 retries, 500ms/1s/2s delays, retries on 5xx and network errors only (not 4xx)
- [ ] Circuit breaker: opens after 5 consecutive failures; half-open after 30s
- [ ] Every log line includes structured fields: `CorrelationId`, `TenantId`, `UserId`, `ServiceName`, `Environment`

---

## WP 1.5 — Database Provisioning

**Sprint:** 1 | **Owner:** DevOps | **Effort:** 1w

### Context

Each microservice owns its own database. No service reads another service's database directly. All databases are provisioned as Aspire container resources in local dev and as managed services in staging/production.

### User stories

**US-1.5.1** — As a developer, all databases start automatically when I run the AppHost so that I never manually manage containers.

**US-1.5.2** — As a developer, each service runs its EF Core migrations automatically on startup so that the schema is always in sync with the code.

**US-1.5.3** — As a system, the `pgvector` extension is available in PostgreSQL so that embedding-based similarity search is ready for Phase 2 AI services.

### Acceptance criteria

- [ ] AppHost provisions the following resources: PostgreSQL 16 (with `pgvector` extension enabled), Redis 7, MongoDB 7, RabbitMQ 3.13
- [ ] Each service has its own named PostgreSQL database: `lms_identity`, `lms_courses`, `lms_enrollment`, `lms_progress`, `lms_assessment`
- [ ] ContentService uses MongoDB database: `lms_content`
- [ ] Redis is shared across: Gateway (rate limit counters), AssessmentService (session state)
- [ ] EF Core migrations run via `MigrateDbAsync()` on service startup — never applied manually
- [ ] Connection strings are never hardcoded; all injected via Aspire `WithReference()` or environment variables
- [ ] A `docker-compose.override.yml` exists for developers who prefer plain Docker over Aspire
- [ ] Staging/production uses: Azure Database for PostgreSQL Flexible Server, Azure Cache for Redis, Azure Cosmos DB (MongoDB API), Azure Service Bus (RabbitMQ-compatible)

### Database ownership map

| Service | Database | Engine | Notes |
|---|---|---|---|
| IdentityService | lms_identity | PostgreSQL | User profiles, tenant config |
| CourseService | lms_courses | PostgreSQL | Courses, modules, lessons |
| ContentService | lms_content | MongoDB | Media metadata, SCORM packages |
| EnrollmentService | lms_enrollment | PostgreSQL | Enrollments, seats, waitlist |
| ProgressService | lms_progress | PostgreSQL | Progress records, xAPI statements |
| AssessmentService | lms_assessment | PostgreSQL + Redis | Questions, attempts; Redis for sessions |

---

## WP 1.6 — Event Bus (RabbitMQ + MassTransit)

**Sprint:** 1–2 | **Owner:** Backend | **Effort:** 1w

### Context

All inter-service async communication uses RabbitMQ via MassTransit. Services publish and consume strongly-typed message contracts defined in `LMS.Contracts`. No service calls another service's HTTP API for async flows.

### User stories

**US-1.6.1** — As a developer, I publish a domain event by calling `IPublishEndpoint.Publish<T>()` and the bus handles routing so that I don't configure exchanges or queues manually.

**US-1.6.2** — As a system, if a consumer fails to process a message it is automatically retried and eventually moved to a dead-letter queue so that no events are silently lost.

**US-1.6.3** — As a developer, I can see all in-flight messages and dead-letter queues in the RabbitMQ management UI so that I can debug event flows.

### Acceptance criteria

- [ ] MassTransit is configured in `ServiceDefaults` using `AddMassTransit()` with RabbitMQ transport
- [ ] All event contracts live in `LMS.Contracts` as `record` types with `Guid` IDs and `DateTimeOffset` timestamps
- [ ] Retry policy: 3 immediate retries, then 3 delayed retries (5min, 15min, 30min) before dead-letter
- [ ] Dead-letter queue exists per consumer; alerts fire when DLQ depth > 10 messages
- [ ] RabbitMQ management UI accessible at `http://localhost:15672` in local dev
- [ ] Consumer idempotency: all consumers check for duplicate `MessageId` before processing (using Redis or DB dedup table)
- [ ] Message contracts are versioned; backward-compatible changes use optional fields

### Phase 1 event contracts

```csharp
// LMS.Contracts/Events/

record UserRegistered(Guid UserId, string Email, string TenantId, DateTimeOffset OccurredAt);
record UserEnrolled(Guid UserId, Guid CourseId, string PlanType, string TenantId, DateTimeOffset OccurredAt);
record LessonCompleted(Guid UserId, Guid LessonId, Guid CourseId, float WatchPercent, string TenantId, DateTimeOffset OccurredAt);
record CourseCompleted(Guid UserId, Guid CourseId, string TenantId, DateTimeOffset OccurredAt);
record AssessmentSubmitted(Guid UserId, Guid AssessmentId, Guid CourseId, float Score, bool Passed, string TenantId, DateTimeOffset OccurredAt);
record NotificationRequested(Guid UserId, string Channel, string TemplateId, Dictionary<string,string> Params, string TenantId, DateTimeOffset OccurredAt);
```

---

## WP 1.7 — Feature Flags

**Sprint:** 1 | **Owner:** Lead Dev | **Effort:** 0.5w

### Context

Feature flags allow Tier 2–4 features to be deployed to production but only activated for specific users, tenants, or percentages of traffic. This prevents incomplete features from being visible and enables safe rollouts.

### User stories

**US-1.7.1** — As a product manager, I can enable a feature for a specific tenant without a code deployment so that I can run controlled rollouts.

**US-1.7.2** — As a developer, I wrap any Tier 3/4 feature in `IFeatureManager.IsEnabledAsync("FeatureName")` so that it can be toggled remotely.

### Acceptance criteria

- [ ] `Microsoft.FeatureManagement.AspNetCore` is configured in `ServiceDefaults`
- [ ] Azure App Configuration is the backend; local dev uses `appsettings.json` overrides
- [ ] Feature filter `TenantFilter` enables per-tenant flag activation using `TenantId` from the request context
- [ ] Feature filter `PercentageFilter` enables gradual rollout by percentage
- [ ] All Phase 3/4 features are wrapped in a flag from the moment they are merged to main
- [ ] Flag states are cached with a 30-second TTL to avoid per-request round-trips to App Configuration

---

## WP 2.1 — Identity Service

**Sprint:** 3–4 | **Owner:** Backend | **Effort:** 2w

### Context

`LMS.IdentityService` does not perform authentication (Keycloak owns that). It manages the **user profile** and **tenant configuration** that supplements the Keycloak identity. It is the source of truth for display names, avatars, preferences, and tenant settings.

### User stories

**US-2.1.1** — As a student, after registering via Keycloak I have a profile with my name, avatar, timezone, and language preference so that the platform is personalised.

**US-2.1.2** — As an instructor, I can update my profile bio and profile picture so that students know who I am.

**US-2.1.3** — As an org admin, I can manage users in my tenant — invite, deactivate, and change roles — so that I control who has access.

**US-2.1.4** — As a system, when a new Keycloak user first calls any API a profile is auto-provisioned via a `UserRegistered` event so that no manual setup is needed.

**US-2.1.5** — As an org admin, I can configure my tenant's settings (name, logo URL, timezone, allowed email domains) so that the platform reflects my organisation.

**US-2.1.6** — As a system, every database row in every service is scoped to a `TenantId` so that data is fully isolated between organisations.

### Acceptance criteria

- [ ] `POST /api/identity/profile` — creates or updates profile from JWT claims (upsert on first login)
- [ ] `GET /api/identity/profile/me` — returns current user's profile
- [ ] `PUT /api/identity/profile/me` — updates display name, bio, avatar URL, timezone, language
- [ ] `GET /api/identity/users` — org admin only; returns paginated list of users in their tenant
- [ ] `POST /api/identity/users/invite` — sends invite email via NotificationWorker; creates pending user record
- [ ] `PATCH /api/identity/users/{userId}/role` — org admin only; changes user's Keycloak role via Admin API
- [ ] `PATCH /api/identity/users/{userId}/deactivate` — sets user as inactive; Keycloak account disabled
- [ ] `GET /api/identity/tenant` — returns tenant config (name, logo, timezone, allowed domains)
- [ ] `PUT /api/identity/tenant` — org admin only; updates tenant config
- [ ] All endpoints enforce `TenantId` from `X-Tenant-Id` header — users cannot access other tenants' data
- [ ] EF Core global query filter applies `TenantId` to all entities automatically
- [ ] `UserRegistered` event published when a new profile is created

### Key data model

```
User { Id, KeycloakId, TenantId, DisplayName, Email, AvatarUrl, Bio, Timezone, Language, IsActive, CreatedAt }
Tenant { Id, Name, LogoUrl, Timezone, AllowedEmailDomains[], Plan, CreatedAt }
UserInvite { Id, TenantId, Email, Role, InvitedBy, ExpiresAt, AcceptedAt }
```

---

## WP 2.2 — Course Service

**Sprint:** 3–5 | **Owner:** Backend | **Effort:** 3w

### Context

`LMS.CourseService` is the content catalogue. It manages the hierarchy: Course → Section → Lesson. It does not serve media files (that is ContentService). It exposes both an instructor-facing authoring API and a student-facing catalogue API.

### User stories

**US-2.2.1** — As an instructor, I can create a course with a title, description, thumbnail, category, tags, and difficulty level so that students can find and understand it.

**US-2.2.2** — As an instructor, I can structure my course into sections and lessons so that content is logically organised.

**US-2.2.3** — As an instructor, I can set prerequisites on a course so that students must complete earlier courses first.

**US-2.2.4** — As an instructor, I can publish or unpublish a course so that I control visibility to students.

**US-2.2.5** — As a student, I can browse the public course catalogue filtered by category, tag, difficulty, and language so that I find relevant courses.

**US-2.2.6** — As a student, I can view a course detail page with syllabus, instructor info, rating, and enrollment count before enrolling.

**US-2.2.7** — As an instructor, I can duplicate a course to use as a template so that I don't start from scratch.

**US-2.2.8** — As an instructor, every change to a published course is versioned so that enrolled students are not disrupted mid-course.

### Acceptance criteria

**Instructor API (requires `instructor` or `admin` role):**

- [ ] `POST /api/courses` — create draft course; returns `CourseId`
- [ ] `PUT /api/courses/{id}` — update course metadata
- [ ] `POST /api/courses/{id}/publish` — sets status to `Published`; emits `CoursePublished` event
- [ ] `POST /api/courses/{id}/unpublish` — sets status to `Draft`
- [ ] `POST /api/courses/{id}/duplicate` — deep copies course structure (not media files)
- [ ] `POST /api/courses/{id}/sections` — add section
- [ ] `PUT /api/courses/{id}/sections/{sectionId}` — update section title/order
- [ ] `DELETE /api/courses/{id}/sections/{sectionId}` — remove section (fails if lessons exist)
- [ ] `POST /api/courses/{id}/sections/{sectionId}/lessons` — add lesson (links to a ContentItem ID)
- [ ] `PUT /api/courses/{id}/sections/{sectionId}/lessons/{lessonId}` — update lesson title/order/free-preview flag
- [ ] `DELETE /api/courses/{id}/sections/{sectionId}/lessons/{lessonId}` — remove lesson

**Student/Public API:**

- [ ] `GET /api/courses` — paginated catalogue; filters: `category`, `tags[]`, `difficulty`, `language`, `instructorId`; sorts: `newest`, `popular`, `rating`; no auth required
- [ ] `GET /api/courses/{id}` — course detail with sections and lesson titles (no content URLs); no auth required
- [ ] `GET /api/courses/{id}/syllabus` — full section/lesson structure including durations; no auth required

**Business rules:**

- [ ] A lesson cannot be reordered above a prerequisite lesson
- [ ] Deleting a published course is not allowed — only unpublish
- [ ] Course versioning: a `CourseVersion` snapshot is created on every publish; enrolled students stay on the version they started
- [ ] `TenantId` applied to all queries; instructors only manage courses in their own tenant
- [ ] Course thumbnail URL is stored as a reference to ContentService; not uploaded directly to CourseService

### Key data model

```
Course { Id, TenantId, InstructorId, Title, Description, ThumbnailContentId, Category, Tags[], Difficulty, Language, Status(Draft/Published), Version, CreatedAt, PublishedAt }
CourseSection { Id, CourseId, TenantId, Title, Order }
CourseLesson { Id, SectionId, CourseId, TenantId, Title, ContentItemId, DurationSeconds, IsFreePreview, Order }
CoursePrerequisite { CourseId, RequiresCourseId }
CourseVersion { Id, CourseId, Version, SnapshotJson, CreatedAt }
```

---

## WP 2.3 — Content Delivery Service

**Sprint:** 3–6 | **Owner:** Backend | **Effort:** 3w

### Context

`LMS.ContentService` manages all media assets: videos, PDFs, SCORM packages, and images. It stores metadata in MongoDB and binary files in S3-compatible object storage. Video files are transcoded to HLS adaptive bitrate. Files are served via CDN signed URLs with short TTLs.

### User stories

**US-2.3.1** — As an instructor, I can upload a video file and it is automatically transcoded to multiple quality levels (HLS) so that students get adaptive playback.

**US-2.3.2** — As an instructor, I can upload a PDF, SCORM package, or image and get back a content ID to reference in lessons.

**US-2.3.3** — As a student, I can stream a video at the quality level appropriate for my connection speed so that playback is smooth.

**US-2.3.4** — As a student, I receive a short-lived signed URL for each content item so that I cannot share direct links to paid content.

**US-2.3.5** — As a system, video playback position is tracked so that students can resume from where they left off.

**US-2.3.6** — As an instructor, I can see the processing status of my uploaded content so that I know when it is ready to use in a lesson.

### Acceptance criteria

- [ ] `POST /api/content/upload` — returns a pre-signed S3 PUT URL; client uploads directly to S3; max file size 4GB
- [ ] `POST /api/content/{id}/process` — triggers async transcoding job (via background service); returns `202 Accepted`
- [ ] Transcoding produces: 360p, 720p, 1080p HLS streams + thumbnail image at 10s mark; stored in S3
- [ ] `GET /api/content/{id}` — returns metadata: type, status (`Pending/Processing/Ready/Failed`), duration, thumbnail URL
- [ ] `GET /api/content/{id}/stream` — returns a signed HLS manifest URL (TTL = 15 minutes); requires valid enrollment (checked against EnrollmentService via internal HTTP call)
- [ ] `GET /api/content/{id}/download` — returns a signed S3 GET URL (TTL = 5 minutes) for PDF/SCORM; requires valid enrollment
- [ ] `POST /api/content/{id}/progress` — student posts `{ positionSeconds, totalSeconds }` every 30s during playback; stored for resume and used to calculate `WatchPercent` for `LessonCompleted` event
- [ ] `DELETE /api/content/{id}` — instructor only; marks as deleted; S3 file removed async (only if no lessons reference it)
- [ ] All signed URLs include `X-User-Id` for audit; signed with CloudFront key pair or S3 presign
- [ ] Metadata stored in MongoDB with fields: `{ id, tenantId, uploadedBy, type, filename, s3Key, status, durationSeconds, thumbnailUrl, hlsManifestUrl, sizeBytes, createdAt }`
- [ ] Transcoding is idempotent — re-triggering an already-transcoded item is a no-op
- [ ] Failed transcoding sends `ContentProcessingFailed` event; instructor notified via NotificationWorker

### Open decisions

- [ ] **OD-2.3.a:** Use AWS MediaConvert, FFmpeg on a k8s job, or Cloudflare Stream for transcoding? → Recommendation: FFmpeg on k8s Job for cost control; MediaConvert as production option.
- [ ] **OD-2.3.b:** Store video progress in ContentService or ProgressService? → Decision: ContentService stores raw playback position (resume); ProgressService stores completion percentage (learning record).

---

## WP 2.4 — Enrollment Service

**Sprint:** 3–4 | **Owner:** Backend | **Effort:** 2w

### Context

`LMS.EnrollmentService` manages who has access to which course. In Phase 1, payment is stubbed — enrollment can be free or require a `PaymentReference` that will be validated by PaymentService in Phase 2.

### User stories

**US-2.4.1** — As a student, I can enroll in a free course instantly so that I can start learning immediately.

**US-2.4.2** — As a student enrolling in a paid course, my enrollment is activated only after payment is confirmed.

**US-2.4.3** — As a student, I cannot enroll in a course if I have not completed its prerequisites.

**US-2.4.4** — As an instructor or admin, I can manually enroll a student in a course so that I can grant access outside the normal flow.

**US-2.4.5** — As a system, ContentService and ProgressService can verify enrollment status for a user+course pair so that only enrolled students access content.

**US-2.4.6** — As an admin, I can set a seat limit on a course so that enrollment is capped at a maximum number of students.

**US-2.4.7** — As a student, if a course is at capacity I can join the waitlist so that I am notified when a seat becomes available.

### Acceptance criteria

- [ ] `POST /api/enrollments` — body: `{ courseId, paymentReference? }`; validates prerequisites; creates enrollment; publishes `UserEnrolled` event
- [ ] `GET /api/enrollments/me` — returns all enrollments for the current user with status and progress summary
- [ ] `GET /api/enrollments/{courseId}/students` — instructor only; returns enrolled students with progress %
- [ ] `POST /api/enrollments/manual` — admin/instructor only; enrolls a specific `userId` into `courseId`
- [ ] `DELETE /api/enrollments/{id}` — admin only; unenrolls student; does not delete progress records
- [ ] `GET /api/enrollments/check?courseId={id}&userId={id}` — internal endpoint (no auth bypass, uses service-to-service header); returns `{ enrolled: bool, status: string }`
- [ ] `POST /api/enrollments/{courseId}/waitlist` — adds student to waitlist if course is full
- [ ] Enrollment states: `Pending` (payment not confirmed), `Active`, `Completed`, `Suspended`
- [ ] Prerequisite check: calls CourseService to get prerequisites; checks EnrollmentService own DB for completion — no cross-service DB access
- [ ] Seat limit enforced atomically using PostgreSQL advisory lock or `SELECT FOR UPDATE` on seat counter
- [ ] `UserEnrolled` event published on transition to `Active`

### Key data model

```
Enrollment { Id, UserId, CourseId, TenantId, Status, PaymentReference, EnrolledAt, CompletedAt, ExpiresAt }
WaitlistEntry { Id, UserId, CourseId, TenantId, JoinedAt, Position }
CourseCapacity { CourseId, TenantId, MaxSeats, CurrentEnrolled }
```

---

## WP 2.5 — Progress Service (xAPI)

**Sprint:** 5–6 | **Owner:** Backend | **Effort:** 3w

### Context

`LMS.ProgressService` is the learning record store. It tracks which lessons have been completed, overall course completion percentage, and emits xAPI-compatible statements. It is the service that decides when a course is "completed" and triggers downstream events.

### User stories

**US-2.5.1** — As a student, my progress is automatically saved as I complete lessons so that the platform knows where I am in a course.

**US-2.5.2** — As a student, I can see my overall completion percentage per course so that I know how far along I am.

**US-2.5.3** — As a system, when a student completes the last required lesson in a course the `CourseCompleted` event is emitted automatically.

**US-2.5.4** — As an instructor, I can see per-lesson completion rates across all students so that I can identify where students drop off.

**US-2.5.5** — As an LMS admin, all completion events are stored as xAPI statements so that they can be exported to an external Learning Record Store (LRS).

**US-2.5.6** — As a student, optional lessons do not count towards course completion so that I am not blocked by bonus content.

### Acceptance criteria

- [ ] `POST /api/progress/lessons/{lessonId}/complete` — marks lesson as complete; calculates new course completion %; publishes `LessonCompleted` event; if 100% publishes `CourseCompleted`
- [ ] `GET /api/progress/courses/{courseId}` — returns: `{ completionPercent, lessonsCompleted, totalLessons, lastAccessedAt, completedAt? }`
- [ ] `GET /api/progress/courses/{courseId}/lessons` — returns per-lesson completion status for current user
- [ ] `GET /api/progress/courses/{courseId}/analytics` — instructor only; returns per-lesson completion rate across all students
- [ ] `GET /api/progress/me` — returns summary of all enrolled courses with completion % for current user
- [ ] Completion % = `completedRequiredLessons / totalRequiredLessons * 100`; optional lessons excluded from denominator
- [ ] `LessonCompleted` event includes: `UserId`, `LessonId`, `CourseId`, `WatchPercent`, `TenantId`, `OccurredAt`
- [ ] `CourseCompleted` event published exactly once per user+course (idempotent; duplicate completions ignored)
- [ ] xAPI statement generated for every `LessonCompleted` and `CourseCompleted` and stored in `XApiStatement` table
- [ ] xAPI statements conform to ADL xAPI 1.0.3 spec (actor, verb, object, result, context)
- [ ] Consumer subscribes to `ContentService` playback progress updates to get `WatchPercent` without a direct HTTP call

### Key data model

```
LessonProgress { Id, UserId, LessonId, CourseId, TenantId, Status(NotStarted/InProgress/Completed), WatchPercent, CompletedAt, LastAccessedAt }
CourseProgress { Id, UserId, CourseId, TenantId, CompletionPercent, CompletedAt, LastAccessedAt }
XApiStatement { Id, UserId, TenantId, Verb, ObjectId, ObjectType, ResultScore, ResultCompletion, ContextCourseId, RawJson, OccurredAt }
```

---

## WP 2.6 — Notification Worker

**Sprint:** 5–6 | **Owner:** Backend | **Effort:** 1.5w

### Context

`LMS.NotificationWorker` is a MassTransit consumer that reacts to domain events and sends notifications via email (SendGrid), push (Firebase FCM), or in-app. In Phase 1 only email is required.

### User stories

**US-2.6.1** — As a student, I receive a welcome email when I enroll in a course so that I know how to get started.

**US-2.6.2** — As a student, I receive a congratulations email when I complete a course so that my achievement is acknowledged.

**US-2.6.3** — As a student, I receive an email invitation when an org admin invites me to the platform.

**US-2.6.4** — As a system, if an email fails to send it is retried without re-triggering the business logic that caused the notification.

**US-2.6.5** — As a developer, I can add a new notification type by adding a new MassTransit consumer and template without modifying existing code.

### Acceptance criteria

- [ ] Consumes `UserEnrolled` → sends "Welcome to [CourseName]" email
- [ ] Consumes `CourseCompleted` → sends "Congratulations, you completed [CourseName]!" email
- [ ] Consumes `NotificationRequested` → sends the specified template with the provided parameters (generic dispatch)
- [ ] Email templates stored as Razor `.cshtml` files; rendered server-side before sending to SendGrid
- [ ] SendGrid integration: uses transactional API (not SMTP); API key from environment variable
- [ ] If SendGrid returns 4xx (not 429): log error, do not retry (invalid recipient)
- [ ] If SendGrid returns 5xx or 429: retry up to 5 times with exponential backoff via MassTransit retry policy
- [ ] Every sent notification logged to `NotificationLog` table: `{ Id, UserId, TenantId, Channel, TemplateId, Status, SentAt, Error }`
- [ ] Unsubscribe link included in all marketing emails (not transactional); preference stored in `UserNotificationPreference` table

### Phase 1 email templates

| Template ID | Trigger event | Subject |
|---|---|---|
| `enrollment_welcome` | `UserEnrolled` | "You're enrolled in {CourseName}" |
| `course_completed` | `CourseCompleted` | "Congratulations! You completed {CourseName}" |
| `user_invite` | `NotificationRequested` (templateId = `user_invite`) | "You've been invited to {TenantName} LMS" |
| `content_failed` | `ContentProcessingFailed` | "Your upload failed: {FileName}" |

---

## WP 3.1 — Assessment Service

**Sprint:** 7–8 | **Owner:** Backend | **Effort:** 3w

### Context

`LMS.AssessmentService` manages quizzes and assessments attached to lessons or courses. In Phase 1 it supports multiple-choice questions (MCQ) and true/false with automatic grading. Adaptive assessment and LLM grading are Phase 2 additions.

### User stories

**US-3.1.1** — As an instructor, I can create a quiz with multiple-choice and true/false questions so that I can test student understanding.

**US-3.1.2** — As an instructor, I can set a passing score, time limit, and maximum number of attempts for a quiz.

**US-3.1.3** — As a student, I can take a quiz and receive my score immediately after submission.

**US-3.1.4** — As a student, if I fail a quiz I can retry up to the maximum attempts set by the instructor.

**US-3.1.5** — As a student, a timed quiz automatically submits when the timer expires so that I cannot take unlimited time.

**US-3.1.6** — As an instructor, I can see per-question analytics (% correct, average score) across all students so that I can identify weak areas.

**US-3.1.7** — As an instructor, I can build a question bank and randomly sample questions per attempt so that students get varied quizzes.

### Acceptance criteria

**Instructor API:**

- [ ] `POST /api/assessments` — create assessment: `{ courseId, lessonId?, title, passingScore, timeLimitSeconds?, maxAttempts, isRandomised, questionSampleSize? }`
- [ ] `POST /api/assessments/{id}/questions` — add question: `{ type: MCQ|TrueFalse, prompt, options[], correctOptionIndex, explanation?, points }`
- [ ] `PUT /api/assessments/{id}/questions/{qId}` — update question
- [ ] `DELETE /api/assessments/{id}/questions/{qId}` — remove question
- [ ] `GET /api/assessments/{id}/analytics` — returns per-question: `{ questionId, prompt, correctRate, avgScore }`

**Student API:**

- [ ] `POST /api/assessments/{id}/sessions` — start a session; validates attempt count; samples questions if randomised; stores session in Redis with TTL = `timeLimitSeconds + 60s`; returns `{ sessionId, questions[], expiresAt }`
- [ ] `POST /api/assessments/sessions/{sessionId}/submit` — accepts `{ answers: [{ questionId, selectedOptionIndex }] }`; grades MCQ/TrueFalse automatically; calculates score; stores result; publishes `AssessmentSubmitted` event; returns `{ score, passed, correctAnswers[], explanation[] }`
- [ ] `GET /api/assessments/{id}/attempts/me` — returns student's attempt history with scores
- [ ] Session auto-submit: a background job checks Redis for expired sessions and auto-submits with answers recorded up to that point
- [ ] If `maxAttempts` exceeded: `POST /sessions` returns `403 Forbidden`
- [ ] Questions with `correctOptionIndex` never exposed to student until after submission
- [ ] `AssessmentSubmitted` event published: `{ UserId, AssessmentId, CourseId, Score, Passed, TenantId, OccurredAt }`

### Key data model

```
Assessment { Id, CourseId, LessonId?, TenantId, InstructorId, Title, PassingScore, TimeLimitSeconds, MaxAttempts, IsRandomised, QuestionSampleSize }
Question { Id, AssessmentId, TenantId, Type, Prompt, Options[]{text, isCorrect}, Explanation, Points, Order }
AssessmentSession { Id, AssessmentId, UserId, TenantId, Questions[], StartedAt, ExpiresAt, SubmittedAt }  [Redis]
AssessmentAttempt { Id, AssessmentId, SessionId, UserId, TenantId, Score, Passed, Answers[], StartedAt, SubmittedAt }
```

---

## WP 7.5 — Certificate Service (basic)

**Sprint:** 7–8 | **Owner:** Backend | **Effort:** 2w

### Context

`LMS.CertificateService` generates a PDF certificate when a student completes a course. In Phase 1 this is a simple PDF with student name, course name, completion date, and a verification code. Open Badges 3.0 and blockchain anchoring are Phase 4 additions.

### User stories

**US-7.5.1** — As a student, I automatically receive a certificate when I complete a course so that I have proof of completion.

**US-7.5.2** — As a student, I can download my certificate as a PDF from my profile at any time.

**US-7.5.3** — As a third party, I can verify a certificate is genuine by visiting a public URL with the certificate's verification code.

### Acceptance criteria

- [ ] Consumes `CourseCompleted` event → generates PDF certificate → stores in S3 → records in DB
- [ ] `GET /api/certificates/me` — returns all certificates for the current user with download URLs
- [ ] `GET /api/certificates/{id}/download` — returns short-lived (1 hour) signed S3 URL for the PDF
- [ ] `GET /verify/{verificationCode}` — **public, no auth** — returns `{ valid: true/false, studentName, courseName, completedAt, issuedBy }` as JSON and a simple HTML page
- [ ] Certificate PDF contains: student display name, course title, completion date, instructor name, verification code (UUID), and platform logo
- [ ] PDF generated using a Razor template rendered to HTML then converted to PDF (QuestPDF or PuppeteerSharp)
- [ ] Verification code is a UUID stored in `Certificate.VerificationCode`; not guessable
- [ ] A certificate is issued only once per user+course (idempotent consumer)

### Key data model

```
Certificate { Id, UserId, CourseId, TenantId, VerificationCode, S3Key, IssuedAt, RevokedAt? }
```

---

## Open Design Decisions

The following decisions must be resolved before or during Sprint 1–2. Each is tagged with the work package it blocks.

| ID | Decision | Blocks | Options | Recommendation |
|---|---|---|---|---|
| OD-1.1.a | Single realm vs per-tenant realm in Keycloak | WP 1.1, 2.1, 9.5 | Single realm + `tenant_id` claim / One realm per tenant | Single realm for Phase 1; per-tenant deferred to Phase 3 |
| OD-1.1.b | MFA enforcement for instructors | WP 1.1 | Optional / Required | Required for instructors; optional for students |
| OD-2.3.a | Video transcoding engine | WP 2.3 | FFmpeg on k8s / AWS MediaConvert / Cloudflare Stream | FFmpeg on k8s Job (Phase 1); evaluate MediaConvert at scale |
| OD-2.3.b | Video playback position ownership | WP 2.3, 2.5 | ContentService owns position / ProgressService owns position | ContentService stores raw position; ProgressService stores completion % |
| OD-2.2.a | Course versioning granularity | WP 2.2 | Snapshot on every publish / Semantic versioning (major/minor) | Snapshot on every publish; enrolled users pinned to their version |
| OD-2.4.a | Free vs paid course distinction in Phase 1 | WP 2.4, 7.1 | No paid courses in Phase 1 / Stub payment with `isFree` flag | Add `isFree` flag to Course; paid enrollment blocked until Phase 2 PaymentService |
| OD-3.1.a | Assessment tied to lesson or to course | WP 3.1, 2.2 | Per-lesson only / Per-course (final exam) / Both | Both: `lessonId` is nullable; if null it is a course-level assessment |
| OD-7.5.a | PDF generation library | WP 7.5 | QuestPDF / PuppeteerSharp / wkhtmltopdf | QuestPDF (native .NET, no headless browser dependency) |

---

## Phase 1 Event Contract Summary

| Event | Publisher | Phase 1 Consumers |
|---|---|---|
| `UserRegistered` | IdentityService | NotificationWorker |
| `UserEnrolled` | EnrollmentService | ProgressService (init record), NotificationWorker |
| `LessonCompleted` | ProgressService | — (Phase 2: Gamification, Recommendation) |
| `CourseCompleted` | ProgressService | CertificateService, NotificationWorker |
| `AssessmentSubmitted` | AssessmentService | ProgressService (update completion if assessment is required) |
| `ContentProcessingFailed` | ContentService | NotificationWorker |
| `NotificationRequested` | Any service | NotificationWorker |

---

## Phase 1 API Surface Summary

| Service | Base path | Auth model |
|---|---|---|
| Gateway (YARP) | `/api/**` | JWT → header forwarding |
| IdentityService | `/api/identity` | JWT; role-gated endpoints |
| CourseService | `/api/courses` | GET public; write = instructor/admin |
| ContentService | `/api/content` | JWT; enrollment-checked for stream/download |
| EnrollmentService | `/api/enrollments` | JWT; role-gated |
| ProgressService | `/api/progress` | JWT; students see own data only |
| AssessmentService | `/api/assessments` | JWT; role-gated |
| CertificateService | `/api/certificates`, `/verify/{code}` | JWT for student endpoints; `/verify` = public |
| NotificationWorker | (no HTTP API — bus consumer only) | — |

---

*Document: LMS Phase 1 Feature Specification · Version 1.0 · April 2026*  
*Next: Phase 2 Feature Specification (Gamification, Adaptive Assessment, AI Tutor, Live Sessions, Payments, Analytics)*
