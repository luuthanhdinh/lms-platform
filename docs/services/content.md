# LMS.ContentService

**Responsibility:** Manages video/PDF/image uploads, persists to Azure Blob Storage, triggers async processing. Publishes ContentUploaded → Worker consumes → publishes ContentProcessingCompleted/Failed.

**Port / Database:** `content` / port 5103 | MongoDB `lms-content` | Azure Blob Storage `lms-content` container (Azurite in dev)

**Architecture decisions:** [ADR-008 (content-pipeline)](../adr/adr-008-content-pipeline.md), [ADR-009 (tenant-mongo-isolation)](../adr/adr-009-tenant-mongo-isolation.md)

---

## Entities

MongoDB documents; all implement `ITenantDocument { Guid TenantId }`.

| Document | Key fields | Notes |
|---|---|---|
| `ContentItem` | Id (Guid), TenantId, UploadedBy, OriginalFileName, MimeType, Type, Status, SizeBytes, StorageKey, DurationSeconds, HlsManifestKey, ThumbnailKey, IsDeleted | Soft delete; indexes on `(TenantId, CreatedAt)`, `(TenantId, Status)`, `(TenantId, UploadedBy)`, unique `(TenantId, Id)` |

**Enums:** `ContentType` (Video|Pdf|Image|Other), `ContentStatus` (Pending|Uploaded|Processing|Ready|Failed)

**Tenant safety:** All repository queries include `Builders<T>.Filter.Eq(d => d.TenantId, tenantId)` predicate. Cross-tenant access returns 404 (leak-safe).

---

## HTTP Endpoints

All routes under `/api/content`. Headers forwarded by gateway: `X-User-Id`, `X-Tenant-Id`, `X-Roles`.

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/content/uploads` | instructor/admin | Multipart upload (file + type field). 201 with ContentItemDto |
| `GET` | `/api/content/{id}` | tenant scope | Fetch content metadata |
| `GET` | `/api/content/{id}/playback` | tenant scope | SAS URI if Status=Ready; TTL 60 min. 409 if not ready |
| `GET` | `/api/content` | tenant scope | List (filters: type, status, uploadedBy; page, pageSize) |
| `DELETE` | `/api/content/{id}` | uploader/admin | Soft delete; 204 |

**Error codes:** 400 (TENANT_REQUIRED), 401, 403 (FORBIDDEN), 404 (NOT_FOUND), 409 (NOT_READY), 413 (PAYLOAD_TOO_LARGE)

---

## Blob Storage

**Container:** `lms-content` (created on startup if missing)

**Path convention (tenant-prefixed):**
```
{tenantId}/{contentItemId}/original/{filename}
{tenantId}/{contentItemId}/hls/master.m3u8
{tenantId}/{contentItemId}/thumbs/poster.jpg
```

**Download URLs:** SAS tokens via `BlobClient.GenerateSasUri(Read, TTL=60min)`. Response header `Content-Disposition: attachment` prevents inline rendering.

**Upload:** Direct multipart POST through gateway → API → Blob Storage. No presigned URLs (Phase 1).

---

## Events

### Published (via MongoDB transactional outbox)

```csharp
public record ContentUploaded(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    Guid UploadedBy, ContentType Type, long SizeBytes,
    DateTimeOffset OccurredAt);

public record ContentProcessingCompleted(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    string HlsManifestUrl, int DurationSeconds, DateTimeOffset OccurredAt);

public record ContentProcessingFailed(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);
```

### Consumers
- **ContentUploaded** → ContentService.Worker (trigger processing)
- **ContentProcessingCompleted** → CourseService (update lesson duration), NotificationWorker
- **ContentProcessingFailed** → NotificationWorker (alert instructor), CourseService

---

## Worker Flow

1. Worker consumes `ContentUploaded` event
2. Load `ContentItem` (tenantId predicate); if Status ≠ Uploaded or IsDeleted → ack & exit (idempotent)
3. Set Status = Processing
4. **Phase 1 stub:** Synthesize HLS manifest key, thumbnail key, duration (no real ffmpeg)
5. On success: Status = Ready, outbox-publish `ContentProcessingCompleted` with SAS URI for manifest
6. On failure: Status = Failed, FailureReason, publish `ContentProcessingFailed`
7. Retry: MassTransit `Intervals(1s, 5s, 30s)` then poison queue

---

## Security

- **MIME whitelist:** video/mp4, video/webm, application/pdf, image/jpeg, image/png
- **Magic-byte sniff:** Verify file header before MIME type trust
- **Filename sanitization:** Regex `[^a-zA-Z0-9\-_\.]` → underscore
- **Size limits:** 5 GiB video, 100 MiB docs (enforced via `RequestSizeLimitAttribute`)
- **SAS download URLs:** Expiry 60 min, no list/write/delete permissions

---

## Project Layout

```
LMS.ContentService/
  Domain/          # Documents, enums, repos, storage & processor interfaces, DTOs
  Infrastructure/  # MongoDB context, repos, Azure Blob client, MassTransit outbox
  Api/             # Minimal APIs, validators, ITenantContext, ContentEndpoints
  Worker/          # IHostedService content processor (4-project pattern)
```

---

## AppHost Wiring

```csharp
var contentDb = mongo.AddDatabase("lms-content");
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var contentBlobs = storage.AddBlobs("content-blobs");

var content = builder.AddProject<LMS_ContentService_Api>("content")
    .WithReference(contentDb).WithReference(rabbitmq).WithReference(contentBlobs)
    .WaitFor(contentDb).WaitFor(contentBlobs).WaitFor(rabbitmq);

var contentWorker = builder.AddProject<LMS_ContentService_Worker>("content-worker")
    .WithReference(contentDb).WithReference(rabbitmq).WithReference(contentBlobs)
    .WaitFor(content);

gateway.WithReference(content);
```

Gateway YARP route `/api/content/{**rest}` → `content` cluster (already configured).
