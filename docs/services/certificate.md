# LMS.CertificateService

**Responsibility:** Issues PDF certificates when students complete courses. Provides public verification endpoint. Idempotent event consumer.

**Port / AppHost:** `certificate` / port 5108 | **Database:** PostgreSQL `lms_certificate`, schema `certificates`

**Storage:** Azure Blob Storage (Azurite in dev) — PDFs at `certificate-pdfs/{tenantId}/{userId}/{certId}.pdf`

**Architecture decisions:** Deterministic certificate ID (v5 UUID from TenantId+UserId+CourseId), QuestPDF Community Edition, idempotent consumer on CourseCompleted

---

## Entities

All inherit `TenantEntity`. Schema: `certificates`.

| Entity | Key fields | Notes |
|---|---|---|
| `Certificate` | UserId, CourseId, CertificateNumber, VerificationCode, Status, PdfStorageKey, IssuedAt, RevokedAt?, CourseName?, LearnerName? | Unique `(TenantId, UserId, CourseId)` — one certificate per student per course; `VerificationCode` used for public `/verify/{code}` endpoint |

**Enums:** `CertificateStatus` (Active|Revoked)

**Indexes:**
- `(TenantId, UserId, Status)` for "list my certificates"
- `(TenantId, VerificationCode)` for public verification

---

## HTTP Endpoints

All routes under `/api/certificates` except `/verify/` (anonymous).
Headers: `X-User-Id`, `X-Tenant-Id`, `X-Roles` forwarded by gateway.

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/certificates/me` | user | List current user's certificates; optional `courseId=` filter |
| `GET` | `/api/certificates/{id}` | user/admin | Get certificate details; owner-or-admin rule |
| `GET` | `/api/certificates/{id}/pdf` | user/admin | Stream PDF file; 410 if revoked |
| `POST` | `/api/certificates/{id}/revoke` | admin/instructor | Revoke certificate; requires `reason` (1–500 chars) |
| `GET` | `/verify/{code}` | anonymous | Public verification link; returns certificate info (no auth required) |

**Error codes:** 400 (TENANT_REQUIRED), 403 (FORBIDDEN), 404 (CERT_NOT_FOUND), 409 (ALREADY_REVOKED), 410 (REVOKED)

---

## Certificate Issuance Flow

### Triggered by CourseCompleted event

1. Consumer receives `CourseCompleted` with optional `CourseName`, `LearnerName` fields
2. Check idempotency: query for existing active certificate → skip if found
3. Compute deterministic `certId` = UUID v5(TenantId, UserId, CourseId) — prevents orphaned blobs on retry
4. Create Certificate record:
   - `CertificateNumber` = `CERT-{now:yyyyMM}-{certId.ToString("N")[..8].ToUpperInvariant()}`
   - `VerificationCode` = Guid.NewGuid() (globally unique)
   - `Status` = Active
   - `CourseName`, `LearnerName` from event payload (may be null in Phase 1)
5. Generate PDF via `ICertificatePdfGenerator` (QuestPDF):
   - Tenant logo (if available)
   - "Certificate of Completion" heading
   - "This certifies that {LearnerName} has completed {CourseName}"
   - Completion date
   - QR code → `https://{frontendBaseUrl}/verify/{VerificationCode}`
6. Upload PDF to blob storage at key `certificate-pdfs/{tenantId}/{userId}/{certId}.pdf`
7. Persist Certificate + PdfStorageKey in DB
8. Publish `CertificateIssued` event with CertificateNumber and VerificationCode (so NotificationWorker can render verification URL without callback)

### Idempotency

Consumer checks `FindByUserCourseAsync(tenantId, userId, courseId)` before issuing.
If found, logs and returns (no double-issuance).
Deterministic certId ensures same blob key on retry.

---

## Events

### Published (via EF Core outbox)

```csharp
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

### Consumed

- **`CourseCompleted`** (ProgressService) → issue certificate and publish CertificateIssued

---

## Public Verification

### GET /verify/{verificationCode}

Anonymous endpoint (YARP routes to "anonymous" policy).

1. Query Certificate by TenantId + VerificationCode
2. If not found → 404
3. If Status == Revoked → return "Certificate revoked on {RevokedAt}: {RevocationReason}"
4. Return certificate summary:
   ```json
   {
     "certificateNumber": "CERT-202604-A1B2C3D4",
     "courseName": "Advanced C#",
     "learnerName": "Jane Doe",
     "issuedAt": "2026-04-27T10:30:00Z",
     "verificationUrl": "https://lms.example.com/verify/..."
   }
   ```

---

## Project Layout

```
LMS.CertificateService/
  Domain/          # Entities, enums, interfaces (ICertificatePdfGenerator, ICertificateRepository, ICertificateStorageService)
  Infrastructure/  # DbContext, EF configs, consumers (CourseCompletedConsumer), outbox
  Api/             # Minimal APIs, CertificateEndpoints
  Migrator/        # IHostedService for EF Core migrations on startup
```

---

## AppHost Wiring

```csharp
var certificateDb = postgres.AddDatabase("lms-certificate");
var certificatePdfs = storage.AddBlobs("certificate-pdfs");

var certificateMigrator = builder.AddProject<Projects.LMS_CertificateService_Migrator>("certificate-migrator")
    .WithReference(certificateDb).WaitFor(certificateDb);

var certificate = builder.AddProject<Projects.LMS_CertificateService_Api>("certificate")
    .WithReference(certificateDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(certificatePdfs)
    .WaitForCompletion(certificateMigrator).WaitFor(certificatePdfs);

gateway.WithReference(certificate);
```

YARP routes `/api/certificates/**` → `certificate` cluster.
YARP routes `/verify/**` → `certificate` cluster with `AuthorizationPolicy: "anonymous"`.
