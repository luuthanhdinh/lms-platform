# LMS.NotificationWorker

**Responsibility:** Event-driven email sender. Consumes 12 Phase 1 events, renders Scriban email templates, sends via SMTP.

**Port / AppHost:** none (worker only) | **Database:** none (Redis only for cache + idempotency)

**Storage:** Redis — `notif:user:{tenantId}:{userId}` (contact cache, 90d TTL), `notif:dedupe:{key}` (idempotency, 7d TTL)

**Email provider (Phase 1):** MailKit SMTP to MailHog (dev) or production SMTP server

**Architecture decisions:** Redis contact cache populated by UserRegistered consumer, idempotency via Redis dedupe keys, Scriban templating (11 email sets), cache-miss handling (drop + warn)

---

## Events Consumed (Phase 1)

| Event | Action | Template | Dedupe key |
|---|---|---|---|
| `UserRegistered` | Welcome email (populate contact cache) | `welcome` | `welcome:{tenantId}:{userId}` (7d) |
| `UserDeactivated` | Account deactivated email | `account-deactivated` | `deactivated:{tenantId}:{userId}` (7d) |
| `CoursePublished` | Course published email | `course-published` | `course-pub:{tenantId}:{courseId}` (7d) |
| `CourseArchived` | Course archived email | `course-archived` | `course-arch:{tenantId}:{courseId}` (7d) |
| `UserEnrolled` | Enrollment confirmation email | `enrollment-confirmed` | `enroll:{tenantId}:{userId}:{courseId}` (7d) |
| `EnrollmentCancelled` | Cancellation email | `enrollment-cancelled` | `cancel:{tenantId}:{userId}:{courseId}` (7d) |
| `LessonCompleted` | Progress email (feature-flagged `Notifications.LessonProgressEmail=false`) | `N/A` | N/A |
| `CourseCompleted` | Completion congratulations email | `course-completed` | `course-done:{tenantId}:{userId}:{courseId}` (7d) |
| `ContentProcessingCompleted` | Content ready email to instructor | `content-processing-completed` | `content-ok:{tenantId}:{contentId}` (7d) |
| `ContentProcessingFailed` | Processing failure alert to instructor | `content-processing-failed` | `content-fail:{tenantId}:{contentId}` (7d) |
| `AssessmentSubmitted` | Assessment result email | `assessment-result` | `assess:{tenantId}:{userId}:{assessmentId}:{occurredAt}` (7d) |
| `CertificateIssued` | Certificate issued email with PDF link | `certificate-issued` | `cert:{tenantId}:{certificateId}` (7d) |

---

## Contact Cache (`Redis`)

**Key format:** `notif:user:{tenantId}:{userId}`
**TTL:** 90 days
**Populated by:** UserRegisteredConsumer (sets email + displayName on every signup)
**Used by:** All email consumers to resolve recipient address

### Cache miss handling

If contact not found when sending email:
1. Log warning: `"Contact not found for user {UserId} in tenant {TenantId}; dropping email"`
2. Return (email dropped — no retry, no poison pill)

This is intentional: contact should exist after UserRegistered, but if user is deleted mid-send, drop silently.

---

## Idempotency (`Redis`)

**Key format:** `notif:dedupe:{key}` (per table above)
**TTL:** 7 days
**Mechanism:** `IIdempotencyService.TryClaimAsync(key, ttl)` — Redis SETNX

Flow:
1. Consumer generates dedupe key
2. Call `TryClaimAsync(key, TimeSpan.FromDays(7))`
3. Returns `false` if key exists → log and return (email already sent)
4. Returns `true` if key set → proceed to send

Prevents duplicate emails on MassTransit redelivery.

---

## Email Templates (Scriban)

Each email set has 3 files:
- `.subject.txt` — subject line (rendered)
- `.html.scriban` — HTML body (rendered)
- `.txt.scriban` — plain text body (rendered)

### Templates (11 sets)

1. **welcome** — `WelcomeViewModel(PlatformName, FullName, LoginUrl)`
2. **account-deactivated** — `AccountDeactivatedViewModel(FullName, PlatformName)`
3. **course-published** — `CoursePublishedViewModel(CourseName, InstructorName, CourseUrl)`
4. **course-archived** — `CourseArchivedViewModel(CourseName)`
5. **enrollment-confirmed** — `EnrollmentConfirmedViewModel(CourseName, StartDate, CourseUrl)`
6. **enrollment-cancelled** — `EnrollmentCancelledViewModel(CourseName)`
7. **course-completed** — `CourseCompletedViewModel(CourseName, FullName, CertificateIssued, CertificateUrl?)`
8. **content-processing-completed** — `ContentProcessingCompletedViewModel(ContentName, UploadUrl)`
9. **content-processing-failed** — `ContentProcessingFailedViewModel(ContentName, Reason)`
10. **assessment-result** — `AssessmentResultViewModel(AssessmentName, Score, Passed, PassingScore)`
11. **certificate-issued** — `CertificateIssuedViewModel(FullName, CourseName, CertificateNumber, VerificationUrl, PdfDownloadUrl)`

Templates support localization (Phase 2) via `{lang}` prefix (e.g., `vi/welcome.html.scriban`).

---

## SMTP Configuration

### appsettings.json

```json
{
  "Notifications": {
    "PlatformName": "LMS Platform",
    "FrontendBaseUrl": "http://localhost:5173",
    "FromAddress": "noreply@lms.local",
    "FromDisplayName": "LMS Notifications"
  },
  "Smtp": {
    "Host": "mailhog",
    "Port": 1025,
    "Username": null,
    "Password": null,
    "UseTls": false
  },
  "FeatureManagement": {
    "Notifications.LessonProgressEmail": false
  }
}
```

### Dev setup (MailHog)

```csharp
// AppHost.cs
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "latest")
    .WithHttpEndpoint(1025, name: "smtp", port: 1025)
    .WithHttpEndpoint(8025, name: "http");

var notifications = builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
    .WithReference(rabbitmq).WithReference(redis).WithReference(mailhog)
    .WaitFor(rabbitmq).WaitFor(redis).WaitFor(mailhog);
```

Access MailHog UI at `http://localhost:8025` to inspect sent emails.

### Production setup

Use environment variables to override:
```bash
Smtp__Host=smtp.sendgrid.net
Smtp__Port=587
Smtp__Username=apikey
Smtp__Password=SG.xxx...
Smtp__UseTls=true
Notifications__FromAddress=hello@yourdomain.com
```

---

## Program.cs

```csharp
using LMS.NotificationWorker.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNotificationWorkerServices();  // Registers MassTransit, Redis, SMTP, templates

var host = builder.Build();
host.Run();
```

### Extension (WorkerExtensions.cs)

Registers:
- MassTransit + RabbitMQ (all consumers auto-wired via reflection)
- Redis client (contact cache + idempotency)
- `IEmailSender` (SmtpEmailSender)
- `IEmailTemplateRenderer` (ScribanEmailTemplateRenderer)
- `IContactCache` (RedisContactCache)
- `IIdempotencyService` (RedisIdempotencyService)
- Options: `NotificationOptions`, `SmtpOptions`

---

## Project Layout

```
LMS.NotificationWorker/
  Consumers/             # 12 consumers (one per Phase 1 event)
  Cache/                 # IContactCache, RedisContactCache
  Idempotency/           # IIdempotencyService, RedisIdempotencyService
  Email/                 # SmtpEmailSender, SmtpOptions
  Templates/             # IEmailTemplateRenderer, ScribanEmailTemplateRenderer
    Email/               # .scriban + .subject.txt files (11 templates)
    ViewModels/          # DTOs for template rendering
  Extensions/            # WorkerExtensions
  Options/               # NotificationOptions
  Program.cs
```

---

## AppHost Wiring

```csharp
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "latest")
    .WithHttpEndpoint(1025, name: "smtp", port: 1025)
    .WithHttpEndpoint(8025, name: "http");

var notifications = builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithReference(mailhog)
    .WaitFor(rabbitmq).WaitFor(redis).WaitFor(mailhog);
```

- No gateway reference (worker only, no HTTP)
- No database (stateless consumer)
- Redis for contact cache + idempotency
- SMTP endpoint for email sending

---

## Error Handling

### Contact not found

If `IContactCache.GetAsync(tenantId, userId)` returns null during send:
- Log warning
- Drop email (return gracefully)
- Do NOT throw or poison message

### SMTP failure

If `IEmailSender.SendAsync()` throws:
- Log error with tenant/user context
- Rethrow → MassTransit retry policy (3 immediate, then 30s intervals)
- If max retries exceeded → move to dead-letter queue

### Template rendering error

If Scriban template fails:
- Log error with template name + view model
- Rethrow → MassTransit retry

---

## Feature Flags

**`Notifications.LessonProgressEmail`** (default: `false`)

When `true`, LessonCompletedConsumer sends progress email (deferred to Phase 2).
When `false`, consumer logs info and returns (current Phase 1 behavior).

```csharp
// LessonCompletedConsumer.cs
if (!await _featureManager.IsEnabledAsync("Notifications.LessonProgressEmail"))
{
    logger.LogInformation("Lesson progress email feature disabled; skipping for lesson {LessonId}", msg.LessonId);
    return;
}
```

---

## Monitoring

### Structured logs

All consumers log with structured scope:
```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["EventType"] = typeof(TEvent).Name,
    ["TenantId"] = message.TenantId,
    ["UserId"] = message.UserId,  // if applicable
    ["CorrelationId"] = context.CorrelationId
});
```

### Metrics (OpenTelemetry)

- Counter: `notif.email.sent` (tags: template, tenant)
- Counter: `notif.email.failed` (tags: template, tenant, reason)
- Counter: `notif.cache.miss` (tags: tenant, userId)
