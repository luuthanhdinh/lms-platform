# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.CertificateService** (Phase 1, service #8)
Port: **5108** · Database: PostgreSQL `lms-certificate` · Schema: `certificates`
Blob storage: Azure Blob (Aspire emulator in dev) · container `certificate-pdfs`
PDF: **QuestPDF** (CLAUDE.md absolute rule — not iTextSharp, not Puppeteer)

ADR references:
- ADR multi-tenancy (TenantEntity + global EF query filter)
- ADR no-direct-HTTP-between-services (MassTransit only)
- ADR-trust-boundary (gateway-only JWT; downstream reads forwarded headers)
- ADR Phase 4 placeholder fields on `Certificate` (Open Badges 3.0 / Blockchain anchor)
  retained as nullable per `docs/entities.md` — NOT wired Phase 1.

> CertificateService follows the standard 4-project layout
> (Domain / Infrastructure / Api / Migrator) — Postgres via EF Core.
> Adds an Azure Blob storage reference for PDF persistence
> (same Aspire `storage` resource already used by ContentService).

> **Gateway:** YARP routes `/api/certificates/{**rest}` (auth) and
> `/verify/{**rest}` (anonymous) → cluster `certificate` already
> exist in `src/gateway/LMS.Gateway/appsettings.json`. NO gateway
> config change required.

---

## 1. Project layout (locked — 4 projects)

```
src/services/LMS.CertificateService/
  LMS.CertificateService.Domain/         # Certificate, BlockchainAnchor, enums, DTOs, validators contract, ICertificatePdfRenderer abstraction
  LMS.CertificateService.Infrastructure/ # CertificateDbContext, repos, MassTransit consumers, IPdfStorage (Azure Blobs), QuestPDF renderer impl, DI extensions
  LMS.CertificateService.Api/            # Minimal API endpoints, validators, Program.cs, header tenancy, anonymous verify endpoint
  LMS.CertificateService.Migrator/       # IHostedService running EF Core migrations once on startup
```

References:
- Domain → `LMS.SharedKernel`
- Infrastructure → Domain, `LMS.Contracts`, `LMS.ServiceDefaults`
- Api → Infrastructure, `LMS.ServiceDefaults`
- Migrator → Infrastructure, `LMS.ServiceDefaults`

NuGet (Infrastructure):
- `Microsoft.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`
- `QuestPDF` (8.x, MIT/Community license — confirm Open Decision #1)
- `Azure.Storage.Blobs` (transitively via `Aspire.Azure.Storage.Blobs` registered in Api)

---

## 2. Entities (LMS.CertificateService.Domain)

Match `docs/entities.md` exactly — `Certificate` already locked there.
Adds `CertificateNumber` (human-readable, unique) and `TemplateId?`
fields requested by the feature spec; both are additive and Phase-1
relevant. `RevokedAt` and `RevocationReason` already in entities.md.
`PdfStorageKey` maps to existing `PdfS3Key` column name in
`docs/entities.md` — we keep the column name `pdf_storage_key`
(rename from `pdf_s3_key` since storage is now Azure Blob, not S3 —
flagged Open Decision #2).

```csharp
public class Certificate : TenantEntity
{
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }

    // Human-readable, unique within tenant. Format: CERT-{yyyy}-{8-char base32 of Id}.
    public string CertificateNumber { get; set; } = default!;

    public Guid VerificationCode { get; set; } = Guid.NewGuid();
    public string PdfStorageKey { get; set; } = default!;        // blob key e.g. "certificates/{tenantId}/{certId}.pdf"
    public Guid? TemplateId { get; set; }                        // null => default template; reserved for tenant branding
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    // Phase 4 placeholders — NOT populated Phase 1
    public string? CredentialJwt { get; set; }
    public Guid? CredentialId { get; set; }
    public int? StatusListIndex { get; set; }
    public BlockchainAnchor? BlockchainAnchor { get; set; }
}

public class BlockchainAnchor   // owned, no separate table
{
    public string? TxHash { get; set; }
    public long? BlockNumber { get; set; }
    public string? Network { get; set; }
    public DateTimeOffset? AnchoredAt { get; set; }
    public AnchorStatus Status { get; set; } = AnchorStatus.NotAnchored;
}

public enum AnchorStatus { NotAnchored, Pending, Confirmed, Failed }
```

### EF Core configuration (`CertificateDbContext.OnModelCreating`)
- Schema `certificates`. Single table `certificates` (snake_case columns).
- Global query filter on `TenantId` for `Certificate` (auto via reflection
  over `TenantEntity` descendants).
- `BlockchainAnchor` mapped via `OwnsOne` (column prefix `anchor_`).
- Concurrency token: `xmin` via `.IsRowVersion()`.
- Conversion: `AnchorStatus` enum stored as `int`.

**Indexes:**
- Unique `(TenantId, UserId, CourseId)` filtered `WHERE revoked_at IS NULL`
  — one active certificate per (user, course) per tenant; revocation
  allows re-issue.
- Unique `(TenantId, CertificateNumber)` — human-readable lookup.
- Unique `(VerificationCode)` (NO tenant prefix — public verify endpoint
  is anonymous and tenant-agnostic; verification code is a 128-bit GUID
  so collision-resistant globally).
- `(TenantId, UserId, IssuedAt DESC)` — list-my-certificates.

### Migration
- Filename: `20260427_001_InitialCertificateSchema`
- Creates schema `certificates`, single table with indexes above plus
  MassTransit outbox/inbox/state tables in `certificates` schema.

---

## 3. Events — `LMS.Contracts/Certificate/`

### NEW event (T1 — events-architect)

```csharp
// src/LMS.Contracts/Certificate/CertificateIssued.cs
public sealed record CertificateIssued(
    Guid EventId,
    Guid TenantId,
    Guid CertificateId,
    Guid UserId,
    Guid CourseId,
    string CertificateNumber,
    Guid VerificationCode,
    DateTimeOffset IssuedAt,
    DateTimeOffset OccurredAt);
```

Rationale for shape:
- Includes `EventId` (matches `CredentialIssued` / `CoursePublished`
  precedent — events that NotificationWorker triggers email on get
  `EventId` for inbox idempotency).
- `CertificateNumber` + `VerificationCode` carried so
  NotificationWorker can render the verification URL without a callback.
- `OccurredAt` mandatory per cross-cutting rule.

**Note re `docs/events.md`:** that doc currently lists
`CredentialIssued` (Phase 4 Open Badges) as the certificate-issuance
event, but the Phase-1 routing table also points it at
NotificationWorker. We split: Phase 1 publishes `CertificateIssued`
(plain PDF certificate); Phase 4 publishes `CredentialIssued` (verifiable
credential). NotificationWorker consumes BOTH. Flagged Open Decision #3.

### Events consumed (wired in Infrastructure)

| Event | Source | Behaviour |
|---|---|---|
| `CourseCompleted` | ProgressService | Issue certificate idempotent on `(UserId, CourseId, TenantId)`. If an active cert exists → no-op. Generate PDF, persist blob, insert row, outbox-publish `CertificateIssued`. Dedupe key per ProgressService note: `(UserId, CourseId)`. Inbox key: `(UserId, CourseId, TenantId)` (no `EventId` on this event). |
| `LessonCompleted` | ProgressService | NOT consumed Phase 1. Eligibility lives in ProgressService (`docs/events.md` shows CertificateService as `LessonCompleted` consumer — superseded by `CourseCompleted` gate in ADR-004). Flagged Open Decision #4. |
| `AssessmentSubmitted` | AssessmentService | NOT consumed Phase 1 — course-level exam pass is folded into ProgressService's `CourseCompleted` decision; CertificateService listens only to the completion event. Flagged Open Decision #5. |
| `CourseArchived` | CourseService | NOT consumed Phase 1 — issued certificates remain valid even if course is archived. |

### Publish flow
- `CourseCompletedConsumer` (Infrastructure):
  1. Inbox-check `(UserId, CourseId, TenantId)` → return if duplicate.
  2. `SELECT … WHERE UserId=@u AND CourseId=@c AND RevokedAt IS NULL`
     (uses tenant filter) → if exists, mark inbox done, return.
  3. Generate `CertificateNumber` =
     `CERT-{IssuedAt:yyyy}-{Crockford-base32(Id, 8 chars)}`.
  4. Render PDF via `ICertificatePdfRenderer.Render(certificate, profile)`
     where `profile` is a static Phase-1 default
     (UserDisplayName placeholder = "Learner"; Open Decision #6).
  5. Upload PDF → `IPdfStorage.UploadAsync(blobKey, stream)`.
  6. Insert `Certificate` row + outbox-publish `CertificateIssued`
     in single EF transaction.
- `POST /api/certificates/{id}/revoke` (admin) → set `RevokedAt`
  + `RevocationReason`, save. Phase 1 does NOT publish a revoke event
  (Open Decision #7). Phase 4 will emit `CredentialRevoked`.

---

## 4. MassTransit + outbox

- `AddEntityFrameworkOutbox<CertificateDbContext>` — outbox/inbox/state
  tables generated by EF migration into `certificates` schema.
- `CourseCompletedConsumer` registered with retry
  `Intervals(1s, 5s, 30s)` then poison.
- Inbox idempotency for `CourseCompleted`: composite key
  `(UserId, CourseId, TenantId)` (no `EventId` on this event).

---

## 5. PDF rendering (QuestPDF)

```csharp
// Domain
public interface ICertificatePdfRenderer
{
    byte[] Render(Certificate certificate, CertificateRenderProfile profile);
}

public record CertificateRenderProfile(
    string LearnerDisplayName,
    string CourseTitle,
    string TenantDisplayName,
    string IssuerName,
    Uri? LogoUrl,
    string VerificationUrl);   // e.g. https://<host>/verify/{verificationCode}
```

- Implementation `QuestPdfCertificateRenderer` in Infrastructure.
- `QuestPDF.Settings.License = LicenseType.Community;` set in DI
  registration (Open Decision #1 — confirm Community licence acceptable).
- Layout: A4 landscape, single page, header with tenant logo, body
  with learner name + course title, footer with `CertificateNumber`,
  `IssuedAt`, and verification URL + QR code (QuestPDF's built-in
  `Image` placeholder; QR generation library Open Decision #8 —
  `QRCoder` proposed).
- LearnerDisplayName / CourseTitle / TenantDisplayName Phase-1 sources:
  hard-coded "Learner" / "Course" / "LMS" placeholders unless future
  ADR adds an outbox-snapshot pattern. Flagged Open Decision #6.

## 6. Blob storage (Azure Blob)

```csharp
public interface IPdfStorage
{
    Task<string> UploadAsync(string blobKey, Stream pdf, CancellationToken ct);
    Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct);
}
```

- Container name: `certificate-pdfs` (Aspire `storage` resource shared
  with ContentService). Aspire will provision in dev via the storage
  emulator.
- Blob key format: `{tenantId}/{certificateId}.pdf`
- Connection: `ConnectionStrings:certificate-pdfs` (Aspire-injected
  via `AddBlobs("certificate-pdfs")` reference on Api project).
- Phase 1 reads PDF on each `GET /pdf` request (no CDN signing).
  Open Decision #9 — short-lived SAS URL vs streaming-through-Api.

---

## 7. HTTP endpoints (LMS.CertificateService.Api)

All authenticated routes mounted under `/api/certificates`.
Headers: `X-User-Id`, `X-Tenant-Id`, `X-Roles`. Missing tenant →
`400 TENANT_REQUIRED`. The `/verify/*` route is anonymous (NO tenant
header expected).

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| GET    | `/api/certificates/{id}`     | tenant-scoped | — | 200 `CertificateDto` | 404 |
| GET    | `/api/certificates/me`       | role: `student` (any auth) | optional `?courseId={cid}` | 200 `CertificateDto[]` | 400 |
| GET    | `/api/certificates/{id}/pdf` | tenant-scoped + (owner OR `instructor`/`admin`) | — | 200 `application/pdf` (streamed) | 403, 404, 410 REVOKED |
| POST   | `/api/certificates/{id}/revoke` | role: `admin` | `RevokeCertificateRequest` | 200 `CertificateDto` (with `RevokedAt`) | 403, 404, 409 ALREADY_REVOKED |
| GET    | `/verify/{verificationCode}` | **anonymous** (no tenant header) | — | 200 `CertificateVerificationDto` | 404, 410 REVOKED |

**Out of scope for Phase 1 (deferred):**
- Manual issuance endpoint (`POST /api/certificates`) — Phase 1 issues
  exclusively via `CourseCompleted` consumer.
- Templates CRUD — Phase 1 uses single hard-coded layout.
- `CredentialRevoked` event publish — Phase 4 Open Badges.
- Re-issue after revocation flow — Phase 1 simply allows new issuance
  because the unique index is filtered on `revoked_at IS NULL`.

### Authorization rules
- `GET /api/certificates/{id}` — tenant filter applies; cross-tenant
  → 404 (leak-safe).
- `GET /api/certificates/me` — scopes `UserId` from `X-User-Id`.
- `GET /api/certificates/{id}/pdf` — student may only download own;
  instructors/admins may download any in their tenant.
- `POST /revoke` — `admin` role only; `instructor` rejected `403`.
- `GET /verify/{code}` — anonymous; performs raw query bypassing
  tenant filter (uses `IgnoreQueryFilters()`); returns ONLY safe
  fields (no `UserId` / `PdfStorageKey`); revoked certs return
  `410 REVOKED` with `RevokedAt`.

### DTOs (Domain)

```csharp
public record CertificateDto(
    Guid Id, Guid UserId, Guid CourseId,
    string CertificateNumber, Guid VerificationCode,
    DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt,
    string? RevocationReason, Guid? TemplateId,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CertificateVerificationDto(
    string CertificateNumber, Guid CourseId,
    DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt,
    bool IsValid);   // false iff revoked

public record RevokeCertificateRequest(string Reason);
```

### Error shape
RFC7807 `ProblemDetails`. Codes (`extensions.code`):
`TENANT_REQUIRED`, `CERT_NOT_FOUND`, `REVOKED`, `ALREADY_REVOKED`,
`FORBIDDEN`, `VALIDATION_FAILED`.

---

## 8. AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`, after the AssessmentService block
and replacing the commented `// var certificateDb = …` line:

```csharp
// CertificateService
var certificateDb = postgres.AddDatabase("lms-certificate");
var certificatePdfs = storage.AddBlobs("certificate-pdfs");

var certificateMigrator = builder.AddProject<Projects.LMS_CertificateService_Migrator>("certificate-migrator")
    .WithReference(certificateDb)
    .WaitFor(certificateDb);

var certificate = builder.AddProject<Projects.LMS_CertificateService_Api>("certificate")
    .WithReference(certificateDb)
    .WithReference(rabbitmq)
    .WithReference(certificatePdfs)
    .WaitForCompletion(certificateMigrator)
    .WaitFor(certificatePdfs);

gateway.WithReference(certificate);
```

Aspire resource name **must** be `certificate` to match the existing
YARP cluster destination `http://certificate`.

### Gateway routes (already present)
`certificate` (auth) and `verify` (anonymous) routes + cluster already
exist — NO change required.

---

## 9. Configuration keys

```
ConnectionStrings:lms-certificate     (Aspire-injected Postgres)
ConnectionStrings:rabbitmq            (Aspire-injected)
ConnectionStrings:certificate-pdfs    (Aspire-injected Azure Blob)
Certificate:VerificationBaseUrl       = "https://lms.local/verify"  (used in PDF QR + URL)
Certificate:DefaultIssuerName         = "LMS Platform"
QuestPdf:LicenseType                  = "Community"
```

`ASPNETCORE_URLS` port `5108` in Api `launchSettings.json`; Aspire
overrides at runtime.

---

## 10. Tests (locked surface)

Unit (Domain / Api / Infrastructure test projects):
- `CertificateNumber` generator: format, uppercase, deterministic
  for a given `Id` + year, no collisions in 10k random IDs sample.
- `RevokeCertificateRequest` validator: `Reason` non-empty, max 500.
- `QuestPdfCertificateRenderer` smoke: returns non-empty `byte[]`,
  starts with `%PDF-` magic bytes, contains `CertificateNumber` text
  via QuestPDF's debug text extraction.

Integration (`LMS.IntegrationTests/CertificateService/`) — Testcontainers
Postgres + RabbitMQ + Azurite + MassTransit harness:
- Tenant isolation: tenant A cannot read tenant B's certificate by id
  or via `/me` (404 / empty list).
- `CourseCompletedConsumer`:
  - Issues exactly one certificate; PDF blob exists; `CertificateIssued`
    published exactly once with matching `CertificateNumber`.
  - Re-deliver same `CourseCompleted` → no duplicate row, no duplicate
    blob, no duplicate `CertificateIssued`.
  - If user already has an active cert → no-op (no new row, no event).
  - After revoke + re-deliver → NEW certificate row issued (filtered
    unique index allows it).
- `GET /api/certificates/{id}/pdf` returns `application/pdf` with
  `%PDF-` body; revoked → 410.
- `POST /revoke` as `admin` flips `RevokedAt`; second revoke → 409.
- `POST /revoke` as `instructor` → 403.
- `GET /verify/{code}` anonymous (no tenant header) returns
  `CertificateVerificationDto`; revoked cert → 410 with `RevokedAt`;
  unknown code → 404; response payload contains NO `UserId`,
  `PdfStorageKey`, or `TenantId`.

Architecture (`LMS.ArchitectureTests`):
- `Certificate` inherits `TenantEntity`.
- `CertificateDbContext` applies global query filter on `Certificate`.
- Api project does NOT reference
  `Microsoft.AspNetCore.Authentication.JwtBearer`.
- Verify endpoint handler explicitly calls `IgnoreQueryFilters()`
  (architecture test pattern: only one method in the assembly may do
  so, and it is named `VerifyAsync`).

Contract (`LMS.ContractTests`):
- `CertificateIssued` shape exactly
  `(EventId, TenantId, CertificateId, UserId, CourseId,
    CertificateNumber, VerificationCode, IssuedAt, OccurredAt)`.

---

## 11. Open decisions (auto-resolved — flagged for human review)

1. **QuestPDF Community licence** chosen — assumes platform usage is
   under the small-business / non-commercial threshold OR a commercial
   licence will be procured before GA. Configurable via
   `QuestPdf:LicenseType`. Confirm before Phase 2 launch.
2. **Column rename** `pdf_s3_key` → `pdf_storage_key` (storage moved
   from S3 to Azure Blob). `docs/entities.md` still says `PdfS3Key` —
   doc fix in T11.
3. **Two events for issuance**: Phase 1 = `CertificateIssued`
   (plain PDF), Phase 4 = `CredentialIssued` (Open Badges 3.0 VC).
   `docs/events.md` currently routes `CredentialIssued` to
   NotificationWorker as the Phase-1 issuance event — supersede.
4. **`LessonCompleted` NOT consumed** — `docs/events.md` lists
   CertificateService as a consumer for "eligibility check" but
   ADR-004 puts completion-% logic in ProgressService. Phase 1 listens
   only to `CourseCompleted`.
5. **`AssessmentSubmitted` NOT consumed** — passing a course-level exam
   does not directly issue a certificate; ProgressService is the only
   gate. Listed under feature spec as "may also trigger" but resolved
   to no-op.
6. **Learner / course / tenant display names** — no direct HTTP allowed,
   no snapshot infrastructure exists yet. Phase 1 renders placeholder
   strings ("Learner", "Course", tenant name from Identity headers
   when available — but for the consumer flow there is no header).
   Long-term fix: enrich `CourseCompleted` with display fields, or add
   an Identity/Course read-model snapshot. Critical Open Decision —
   needs human sign-off before T7 PDF render task starts.
7. **No revoke event in Phase 1** — `CredentialRevoked` exists in
   contracts (Phase 4). Phase 1 revoke is local-only; NotificationWorker
   not informed. Confirm.
8. **QR code library** — `QRCoder` proposed (Apache-2.0). Alternative:
   `Net.Codecrete.QrCodeGenerator`. T7 picks one; lock here so the
   Infrastructure NuGet list is final.
9. **PDF download path** — Phase 1 streams through Api
   (`FileStreamResult`). Phase 2 may switch to short-lived Azure SAS
   URL to offload bandwidth. No contract change needed for the switch.
10. **Verification URL host** — `Certificate:VerificationBaseUrl`
    must point at the gateway host (publicly resolvable). Configured
    per environment.
