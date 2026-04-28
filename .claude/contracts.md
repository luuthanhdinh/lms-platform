# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.NotificationWorker** (Phase 1, service #9)
Type: **Worker** (no HTTP, no DB) — pure event consumer
Project layout: **single-project** (worker has no Domain/Api/Migrator split — no DB, no endpoints)
Path: `src/services/LMS.NotificationWorker/`
Email provider: **MailKit SMTP** (Phase 1) behind `IEmailSender` abstraction
Templating: **Scriban** (`.sbn` files embedded as resources)
Dedupe store: **Redis** (`Aspire.StackExchange.Redis`) — composite key per `(MessageId, ConsumerType)`
Aspire resource name: `notifications` (matches `docs/architecture.md` line 55)

ADR / docs references:
- Multi-tenancy rule: every consumer must read `TenantId` from the message
  (no DbContext to filter — the worker is stateless)
- No-direct-HTTP rule: worker MUST NOT call other LMS services over HTTP;
  every datum it needs must already be on the inbound event
- JWT trust boundary: worker has no inbound HTTP — N/A
- `docs/notification.md`: lists Phase 1 consumers + SMTP/MailKit choice
- `docs/events.md` Phase-1 routing table: NotificationWorker is consumer for
  `UserRegistered`, `UserDeactivated`, `CoursePublished`, `CourseArchived`,
  `UserEnrolled`, `EnrollmentCancelled`, `LessonCompleted`, `CourseCompleted`,
  `ContentProcessingCompleted`, `ContentProcessingFailed`,
  `AssessmentSubmitted`, `CertificateIssued`

> **Decision conflict (Open Decision #1):** `docs/notification.md` lists
> only 4 Phase-1 consumers (`UserRegistered`, `UserEnrolled`,
> `CourseCompleted`, `CredentialIssued`) while `docs/events.md` routing
> table lists 12. Plan locks **events.md** as source of truth (it is
> the authoritative cross-cutting reference). `docs/notification.md`
> will be updated by `docs-writer` in T11. `CredentialIssued` is Phase 4
> per the Certificate-feature contracts split — Phase 1 uses
> `CertificateIssued` instead.

---

## 1. Project layout (locked — single project, no DB)

```
src/services/LMS.NotificationWorker/
  LMS.NotificationWorker.csproj
  Program.cs                          # Host.CreateApplicationBuilder, MassTransit, MailKit, Redis
  Consumers/
    UserRegisteredConsumer.cs
    UserDeactivatedConsumer.cs
    CoursePublishedConsumer.cs
    CourseArchivedConsumer.cs
    UserEnrolledConsumer.cs
    EnrollmentCancelledConsumer.cs
    LessonCompletedConsumer.cs        # gated by feature flag (digest-only Phase 2; Phase 1 = no-op)
    CourseCompletedConsumer.cs
    ContentProcessingCompletedConsumer.cs
    ContentProcessingFailedConsumer.cs
    AssessmentSubmittedConsumer.cs
    CertificateIssuedConsumer.cs
  Email/
    IEmailSender.cs                   # abstraction
    SmtpEmailSender.cs                # MailKit impl
    EmailMessage.cs                   # record (To, Subject, BodyHtml, BodyText, ReplyTo?)
    EmailSendResult.cs
  Templates/
    ITemplateRenderer.cs
    ScribanTemplateRenderer.cs
    Models/
      UserRegisteredModel.cs          # one DTO per template
      UserEnrolledModel.cs
      ...
    Sbn/                              # .sbn files (embedded resources)
      user-registered.subject.sbn
      user-registered.html.sbn
      user-registered.text.sbn
      ... (subject/html/text trio per template)
  Idempotency/
    IIdempotencyStore.cs
    RedisIdempotencyStore.cs          # SET NX EX with 7-day TTL
  Options/
    SmtpOptions.cs                    # Host, Port, Username, Password, FromAddress, FromName, UseStartTls
    NotificationOptions.cs            # FrontendBaseUrl, VerifyBaseUrl, SupportEmail, IdempotencyTtlDays
  Properties/launchSettings.json      # Aspire-managed; no exposed port
```

Project references:
- `LMS.ServiceDefaults`
- `LMS.Contracts`
- `LMS.SharedKernel` (for `Result<T>` if needed; no `TenantEntity` since no DB)

NuGet:
- `MassTransit` + `MassTransit.RabbitMQ`
- `MailKit` (3.x) + `MimeKit`
- `Scriban`
- `Aspire.StackExchange.Redis`
- `Microsoft.FeatureManagement`
- `Polly` (transitive via `MassTransit`; explicit dep for SMTP retry)

---

## 2. Events consumed → actions (LOCKED)

All twelve consumers listen to records already defined in `LMS.Contracts/`
(no new event records are introduced by NotificationWorker).

| # | Event (existing in `LMS.Contracts`) | Template id | Recipient | Action / payload mapping |
|---|---|---|---|---|
| 1 | `UserRegistered` | `user-registered` | `Email` (on event) | Welcome email; CTA → `{FrontendBaseUrl}/onboarding` |
| 2 | `UserDeactivated` | `user-deactivated` | resolved via `UserId` → **OPEN DECISION #2** (no email on event; needs lookup) | Account-deactivation notice |
| 3 | `CoursePublished` | `course-published` | instructor (`InstructorId`) — **OPEN DECISION #2** | Confirmation that course is live; CTA → instructor course page |
| 4 | `CourseArchived` | `course-archived` | instructor + active enrolees — **OPEN DECISION #3** (fan-out) | Phase 1 stub: instructor only |
| 5 | `UserEnrolled` | `user-enrolled` | learner (`UserId`) | Enrollment confirmation; CTA → `{FrontendBaseUrl}/courses/{CourseId}` |
| 6 | `EnrollmentCancelled` | `enrollment-cancelled` | learner (`UserId`) | Cancellation notice |
| 7 | `LessonCompleted` | none Phase 1 | — | NO-OP in Phase 1 (per `notification.md`) — consumer registered but exits early; logged for observability. Gated by feature flag `Notifications.LessonProgressEmail` (default `false`). |
| 8 | `CourseCompleted` | `course-completed` | learner (`UserId`) | Congratulations email; CTA → certificate page once `CertificateIssued` lands (separate email) |
| 9 | `ContentProcessingCompleted` | `content-ready` | uploader (`UploadedBy` is NOT on this event — **OPEN DECISION #4**) | Instructor "your video is ready" notice |
| 10 | `ContentProcessingFailed` | `content-failed` | uploader — same OPEN DECISION #4 | Instructor "processing failed" + reason |
| 11 | `AssessmentSubmitted` | `assessment-submitted` | learner (`UserId`) | Result email (Score / Passed / failed → retry CTA) |
| 12 | `CertificateIssued` | `certificate-issued` | learner (`UserId`) | Certificate email; rendered URL: `{VerifyBaseUrl}/{VerificationCode}`; subject contains `CertificateNumber` |

### Email recipient resolution (Phase 1 strategy — locked)

Workers **must not** make HTTP calls to IdentityService. For events that
don't already carry an email address, Phase 1 uses a **best-effort fallback**:

- `UserRegistered`: email is **on the event** → trivial.
- `UserEnrolled`, `EnrollmentCancelled`, `CourseCompleted`,
  `AssessmentSubmitted`, `CertificateIssued`, `UserDeactivated`,
  `CoursePublished`, `CourseArchived`, `ContentProcessingCompleted`,
  `ContentProcessingFailed`: email NOT on event.

→ **Locked Phase-1 strategy:** Maintain an in-memory + Redis-backed
`IUserContactCache` populated by the `UserRegisteredConsumer`
(writes `(TenantId, UserId) → Email + DisplayName`). Other consumers
read from this cache. **Cache miss → log warning + drop email** (no DLQ).
This is acceptable Phase 1 because every active user passes through
`UserRegistered`. Long-term fix tracked as Open Decision #2.

Cache key: `notif:user:{TenantId}:{UserId}` → JSON `{Email, DisplayName, Language}`.
TTL: 90 days (sliding on read).

### Idempotency (locked)

For every consumer, the dedupe key is:
- If event has `EventId` field → `notif:dedupe:{ConsumerType}:{EventId}`
- Otherwise (e.g. `LessonCompleted`, `UserEnrolled`, `CourseCompleted`,
  `AssessmentSubmitted`) → composite `notif:dedupe:{ConsumerType}:{stable-hash}`
  where `stable-hash = SHA256(canonicalize(MessageHeaders.MessageId ?? message-bytes))`.

Strategy: `SET NX EX 7d` on Redis. If key exists → consumer returns
without sending. Idempotency record is stored **before** SMTP send;
SMTP failure raises and triggers MassTransit retry (which will hit the
idempotency record and short-circuit — accepted: prefer "no email" over
"double email").

---

## 3. Email rendering (locked)

```csharp
public interface ITemplateRenderer
{
    Task<RenderedEmail> RenderAsync<TModel>(
        string templateId, TModel model, string language, CancellationToken ct);
}

public sealed record RenderedEmail(string Subject, string BodyHtml, string BodyText);
```

- Templates are `.sbn` Scriban files, three per template id
  (`{id}.subject.sbn`, `{id}.html.sbn`, `{id}.text.sbn`).
- Embedded as `EmbeddedResource` in csproj.
- Language fallback chain: `{lang}` → `en` → throw.
- Phase 1 ships English (`en`) only; Vietnamese (`vi`) deferred (Open Decision #5).
- **No HTML allowed in user-supplied fields** — Scriban auto-encodes;
  documented and asserted by an architecture test.

```csharp
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record EmailMessage(
    string ToAddress,
    string ToDisplayName,
    string Subject,
    string BodyHtml,
    string BodyText,
    Guid TenantId,        // tagged on logs/metrics for tenant filtering
    string? ReplyTo = null);

public sealed record EmailSendResult(bool Sent, string? ProviderMessageId, string? Error);
```

- `SmtpEmailSender` uses `MailKit` `SmtpClient`, `STARTTLS`,
  Polly retry: 3 attempts, exponential backoff 1/3/9s, on
  `SmtpProtocolException` and `IOException`. Final failure throws →
  MassTransit poisons after retry policy (1/5/30s) is exhausted.

---

## 4. Configuration keys (locked)

```jsonc
{
  "Smtp": {
    "Host": "<aspire mailcatcher in dev>",
    "Port": 1025,
    "Username": "",
    "Password": "",
    "FromAddress": "no-reply@lms.local",
    "FromName": "LMS Platform",
    "UseStartTls": false
  },
  "Notifications": {
    "FrontendBaseUrl": "http://localhost:5173",
    "VerifyBaseUrl":   "http://localhost:5108/verify",
    "SupportEmail":    "support@lms.local",
    "IdempotencyTtlDays": 7,
    "UserContactCacheTtlDays": 90
  },
  "FeatureManagement": {
    "Notifications.LessonProgressEmail": false
  },
  "ConnectionStrings": {
    "rabbitmq": "<aspire>",
    "redis":    "<aspire>"
  }
}
```

`SmtpOptions` validated on startup with `IValidateOptions<SmtpOptions>`
(non-empty host, port in 1..65535, valid `FromAddress`).

---

## 5. AppHost wiring (locked diff against `src/LMS.AppHost/Program.cs`)

```csharp
// Add MailHog for dev SMTP capture
var mail = builder.AddContainer("mail", "mailhog/mailhog", "v1.0.1")
                  .WithHttpEndpoint(8025, name: "ui")
                  .WithEndpoint(1025, name: "smtp", scheme: "tcp");

builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WithEnvironment("Smtp__Host", mail.GetEndpoint("smtp"))
    .WithEnvironment("Smtp__Port", "1025")
    .WaitFor(rabbitmq)
    .WaitFor(redis)
    .WaitFor(mail);
```

No gateway change (no inbound HTTP).

---

## 6. Observability (locked)

- All log entries include `TenantId`, `UserId` (when known), `EventType`,
  `MessageId`, `TemplateId`.
- Metrics (`LMS.NotificationWorker` meter):
  - `notifications.sent`     (counter, tags: `tenant_id`, `template_id`)
  - `notifications.dropped`  (counter, tags: `tenant_id`, `reason` = `cache-miss|dedupe|disabled`)
  - `notifications.failed`   (counter, tags: `tenant_id`, `template_id`, `error_class`)
  - `notifications.duration` (histogram, ms)
- Activity (`OpenTelemetry`) per consumer span: `notif.consume.{event-type}`.

---

## 7. Tests (locked surface)

Unit (`tests/LMS.NotificationWorker.UnitTests/` — new project):
- `ScribanTemplateRenderer`: every template id renders with fixture model;
  asserts `{{ user.email }}` is auto-encoded; missing language falls back to `en`.
- `RedisIdempotencyStore`: `TryClaim` uses `SET NX EX`; second claim returns false.
- Per-consumer "happy path" — given event + cache hit → `IEmailSender.SendAsync`
  invoked once with correct subject + body containing key fields.
- Per-consumer "idempotency" — second consume → no send, no exception.
- `LessonCompletedConsumer` with feature flag off → no send; with flag on → send.

Integration (`tests/LMS.IntegrationTests/NotificationWorker/`) — Testcontainers
RabbitMQ + Redis + MailHog (HTTP API for assertions):
- Publish each Phase-1 event via MassTransit harness → assert one email
  arrives in MailHog with correct subject and recipient.
- Re-publish → only one email present (idempotent).
- `UserRegistered` populates contact cache → subsequent `UserEnrolled`
  for same user uses cached email.
- Cache-miss path: publish `UserEnrolled` for unknown user → no email,
  metric `notifications.dropped{reason=cache-miss}` increments.
- Tenant tag propagation: emails for tenant A vs tenant B logged with
  distinct `TenantId` (asserted via OTEL test exporter).

Architecture (`LMS.ArchitectureTests`):
- `LMS.NotificationWorker` does NOT reference `Microsoft.EntityFrameworkCore`.
- `LMS.NotificationWorker` does NOT reference any other `LMS.*Service.*` project.
- `LMS.NotificationWorker` does NOT contain a `HttpClient` typed reference
  to any other LMS service (regex check on `BaseAddress` config keys).
- Every `IConsumer<T>` reads `TenantId` from the message before logging or
  cache access (Roslyn-syntax test or NetArchTest convention).

Contract (`LMS.ContractTests`):
- No new contracts introduced — but a regression test asserts the
  list of consumed events exactly matches the routing table in
  `docs/events.md` (one test per event type, fails if new consumer
  added without updating docs).

---

## 8. Open decisions (require human sign-off before T6/T7 start)

1. **`notification.md` vs `events.md` consumer list mismatch.** Plan
   locks `events.md` (12 consumers); `docs-writer` updates
   `notification.md` in T11. Confirm `events.md` is the source of truth.
2. **Email lookup for events without `Email` field.** Phase 1 strategy:
   Redis-backed contact cache populated by `UserRegisteredConsumer`;
   cache miss → drop email + warn. Acceptable for Phase 1 (closed user
   base, all users pass through `UserRegistered`). Phase 2 may switch
   to read-model snapshot or `IdentityService.UserContactReadModel`.
   **Critical — confirm before T6 starts.**
3. **`CourseArchived` fan-out to enrolees.** Phase 1 sends instructor-only
   notice. Sending to N enrolees would require an enumeration source
   (currently only EnrollmentService knows them). Defer fan-out to Phase 2
   when EnrollmentService publishes per-enrolment cancellation events
   on archive. Confirm Phase-1 instructor-only is acceptable.
4. **`ContentProcessingCompleted` / `Failed` recipient.** Event has
   `ContentItemId` but not `UploadedBy`. Phase 1 must either (a) extend
   the event record (events-architect change) or (b) store an
   `(ContentItemId → UploadedBy)` cache. **Plan locks (a)** — events-architect
   extends both records with `UploadedBy: Guid` (additive, backwards
   compatible since publisher already has the value). Reflected in T1.
5. **i18n — `vi` templates.** Deferred to Phase 2 with `AiTranslation`
   feature flag. Phase 1 ships `en` only.
6. **DLQ / poison handling.** MassTransit default `_error` queue is
   acceptable; no custom handler in Phase 1. Operations runbook entry
   created in `docs-writer` T11.
7. **Rate limiting / per-tenant throttling.** None Phase 1. SMTP server
   is the only bottleneck; MailHog dev / configurable prod. Phase 2 may
   add per-tenant token bucket.
8. **MailHog vs SmtpDev.** Locked MailHog. Aspire image
   `mailhog/mailhog:v1.0.1`. Confirm.

---

## 9. Contract changes locked

### Event record extensions (events-architect — T1)

**Additive only** — adding nullable / new fields to existing records.

```csharp
// LMS.Contracts/Content/ContentProcessingCompleted.cs
public record ContentProcessingCompleted(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    Guid UploadedBy,                        // NEW (Phase 1)
    string HlsManifestUrl, int DurationSeconds,
    DateTimeOffset OccurredAt);

// LMS.Contracts/Content/ContentProcessingFailed.cs
public record ContentProcessingFailed(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    Guid UploadedBy,                        // NEW (Phase 1)
    string Reason, DateTimeOffset OccurredAt);
```

ContentService publisher must be updated to include `UploadedBy`.
This is in scope for **T1 events-architect** task; the ContentService
change is tracked as a follow-up but does NOT block this feature
because Phase 1 ContentService implementation is not yet shipped
(no upstream consumer drift).

No NEW event records are introduced.

### Documentation updates (docs-writer — T11)
- `docs/notification.md` updated to match the 12-event consumer list.
- `docs/events.md` routing table refreshed if `UploadedBy` field added.
