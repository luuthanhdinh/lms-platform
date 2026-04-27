# Contracts (locked — do not deviate)
_Hash: <to be filled by orchestrator>_

Feature: **LMS.ContentService** (Phase 1, service #4)
Port: **5103** · Database: `lms_content` (MongoDB) · Object storage: **Azure Blob Storage** (Azurite emulator in dev)
ADRs: tenant-isolation (Mongo), content-pipeline (async worker), SAS-URL playback

> ContentService is the only service that uses MongoDB. EF Core absolute rules
> ("inherit TenantEntity", "global query filter") do NOT apply. They are
> replaced with the equivalents in section 2.

---

## 1. Project layout (locked — 4 projects)

```
src/services/LMS.ContentService/
  LMS.ContentService.Domain/          # documents, enums, repository + storage + processor interfaces, DTO records
  LMS.ContentService.Infrastructure/  # MongoDB context, repos, Azure Blob storage client, MassTransit wiring
  LMS.ContentService.Api/             # Minimal API endpoints, validators, Program.cs, header tenancy
  LMS.ContentService.Worker/          # IHostedService background processor (Migrator-equivalent slot)
```

The Worker project replaces the Migrator slot in AppHost: it is registered
as a long-running project (NOT `WaitForCompletion`). MongoDB needs no schema
migration; index initialisation runs on Api startup (see section 3).

References:
- Domain → `LMS.SharedKernel`
- Infrastructure → Domain, `LMS.Contracts`, `LMS.ServiceDefaults`
- Api → Infrastructure, `LMS.ServiceDefaults`
- Worker → Infrastructure, `LMS.ServiceDefaults`

NuGet packages (Infrastructure):
- `Aspire.Azure.Storage.Blobs` (~9.4.1-preview.1.25408.4 to align with other Aspire packages)
- `Azure.Storage.Blobs`
- `MongoDB.Driver`
- `MassTransit.MongoDb` (transactional outbox; if unavailable on target version, fall back to MassTransit's RabbitMQ in-memory outbox + idempotent consumers)

---

## 2. MongoDB documents (LMS.ContentService.Domain)

Tenant safety pattern (Mongo replacement for EF global filter):
- All documents implement `ITenantDocument { Guid TenantId { get; } }`.
- All repository methods MUST take `tenantId` as first parameter and apply
  `Builders<T>.Filter.Eq(d => d.TenantId, tenantId)` on every query.
- Architecture test enforces: every method on every repo accepts `tenantId`.

```csharp
public interface ITenantDocument
{
    Guid TenantId { get; }
}

public abstract class ContentDocument : ITenantDocument
{
    [BsonId] public ObjectId MongoId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();          // surrogate Guid (used in API)
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[BsonCollection("content_items")]
public class ContentItem : ContentDocument
{
    public Guid UploadedBy { get; set; }
    public string OriginalFileName { get; set; } = default!;   // <= 260
    public string MimeType { get; set; } = default!;           // e.g. "video/mp4"
    public ContentType Type { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Pending;
    public long SizeBytes { get; set; }
    public string StorageContainer { get; set; } = default!;   // Azure blob container (e.g. "lms-content")
    public string StorageKey { get; set; } = default!;         // canonical blob path (see section 5)
    public int? DurationSeconds { get; set; }
    public string? HlsManifestKey { get; set; }                // blob path (NOT a public URL)
    public string? ThumbnailKey { get; set; }                  // blob path
    public string? FailureReason { get; set; }
    public bool IsDeleted { get; set; }                        // soft delete
}

public enum ContentType  { Video, Pdf, Image, Other }
public enum ContentStatus { Pending, Uploaded, Processing, Ready, Failed }
```

> Note: `docs/entities.md` shows older shape (`ContentItemId` Guid + `S3Key`).
> Locked shape uses `Id` (Guid) and `StorageKey`. T11 docs-writer reconciles
> `docs/entities.md`.

### Indexes (created at Api startup via `IndexInitializer`)
- `content_items`: `{ TenantId: 1, CreatedAt: -1 }`,
  `{ TenantId: 1, Status: 1 }`,
  `{ TenantId: 1, UploadedBy: 1 }`,
  unique `{ TenantId: 1, Id: 1 }`.

---

## 3. Events — `LMS.Contracts/Content/`

### Already exists (do NOT modify)
`src/LMS.Contracts/Content/ContentProcessingCompleted.cs`:
```csharp
public sealed record ContentProcessingCompleted(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    string HlsManifestUrl, int DurationSeconds, DateTimeOffset OccurredAt);
```

### New records to add (T1)
```csharp
public sealed record ContentUploaded(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    Guid UploadedBy, ContentType Type, long SizeBytes,
    DateTimeOffset OccurredAt);

public sealed record ContentProcessingFailed(
    Guid EventId, Guid ContentItemId, Guid TenantId,
    string Reason, DateTimeOffset OccurredAt);
```

`ContentType` enum lives in `LMS.Contracts.Content` (mirrors Domain enum
values; the two enums use identical underlying ints).

### Publish flow
- Api: on successful upload (multipart bytes received + persisted to Blob
  Storage) → publish `ContentUploaded` via outbox (MongoDB transactional
  outbox — see section 4).
- Worker: consumes `ContentUploaded`, runs processor stub, on success
  publishes `ContentProcessingCompleted`, on failure publishes
  `ContentProcessingFailed`.

> `docs/events.md` currently shows `ContentProcessingCompleted` without
> `EventId`. Locked record (already in repo) carries `EventId`. T11 reconciles
> and adds `ContentUploaded` + `ContentProcessingFailed`.

---

## 4. MassTransit + outbox

- Use **MassTransit MongoDB outbox** (`AddMongoDbOutbox`). Collection name
  `mt_outbox` per tenant-agnostic Mongo db `lms_content`.
- Inbox: consumers idempotent on `EventId`.
- Worker uses MassTransit hosted bus; Api uses publish-only endpoint.

---

## 5. Azure Blob Storage (Azurite in dev)

- **Single container per environment**: `lms-content` (configurable). Created
  on startup if missing (private access).
- **Blob path convention** (tenant-prefixed for blast-radius isolation):
  ```
  {tenantId}/{contentItemId}/original/{filename}
  {tenantId}/{contentItemId}/hls/master.m3u8
  {tenantId}/{contentItemId}/thumbs/poster.jpg
  ```
- **Upload flow (Phase 1)**: API accepts `multipart/form-data` directly.
  Bytes stream through the Api into Blob Storage via `BlobClient.UploadAsync`.
  No client-side presigned upload URL — keeps everything behind the gateway.
- **Download / playback**: Azure SAS tokens
  (`BlobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn)`),
  TTL 60 min for playback (Phase 1 stub — Phase 2 may shorten via CDN signing).
- **Connection**: Aspire injects connection string under resource name
  `"storage"` (binds to `BlobServiceClient` via `Aspire.Azure.Storage.Blobs`).

`IContentStorageService` (Domain interface):

```csharp
public interface IContentStorageService
{
    Task<string> UploadAsync(
        Guid tenantId, Guid contentItemId,
        string fileName, string contentType,
        Stream data, CancellationToken ct = default);

    Task<Uri> GetDownloadUriAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, TimeSpan expiry,
        CancellationToken ct = default);

    Task DeleteAsync(
        Guid tenantId, Guid contentItemId,
        string storageKey, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        Guid tenantId, string storageKey,
        CancellationToken ct = default);   // used by Worker
}
```

Implementation: `AzureBlobContentStorageService` uses `BlobServiceClient`
(injected by `Aspire.Azure.Storage.Blobs`) → `BlobContainerClient("lms-content")`
→ `BlobClient(path)`. `UploadAsync` returns the canonical `storageKey`
(blob path). All methods enforce that the blob path begins with
`{tenantId}/{contentItemId}/`; cross-tenant key access throws
`UnauthorizedAccessException`. `StorageKeyBuilder` is the single producer
of paths.

---

## 6. HTTP endpoints (LMS.ContentService.Api)

All routes mounted under `/api/content`, gateway YARP route already exists
in `appsettings.json` (`/api/content/{**rest}` → `content` cluster).

Headers (forwarded by gateway, never re-validated):
`X-User-Id`, `X-Tenant-Id`, `X-Roles`. Public reads still require
`X-Tenant-Id` (else `400 TENANT_REQUIRED`).

| Method | Path | Auth | Request | 2xx | Errors |
|---|---|---|---|---|---|
| POST | `/api/content/uploads` | role: `instructor`/`admin` | `multipart/form-data` (file + `type` field) | 201 `ContentItemDto` | 400, 401, 403, 413 |
| GET | `/api/content/{id}` | tenant scope | — | 200 `ContentItemDto` | 404 |
| GET | `/api/content/{id}/playback` | tenant scope | — | 200 `PlaybackUrlDto` | 404, 409 (not ready) |
| GET | `/api/content` | tenant scope | query: `type?, status?, uploadedBy?, page=1, pageSize=20` | 200 `Page<ContentItemDto>` | 400 |
| DELETE | `/api/content/{id}` | uploader or admin | — | 204 | 403, 404 |

### DTOs
```csharp
// multipart upload form fields (file part + form fields)
public record UploadFormFields(ContentType Type);

public record ContentItemDto(
    Guid Id, Guid TenantId, Guid UploadedBy,
    string OriginalFileName, string MimeType,
    ContentType Type, ContentStatus Status,
    long SizeBytes, int? DurationSeconds,
    string? HlsManifestUrl, string? ThumbnailUrl,
    string? FailureReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record PlaybackUrlDto(
    Guid ContentItemId, Uri Url, DateTimeOffset ExpiresAt,
    int? DurationSeconds);

public record Page<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
```

### Validation rules
- `FileName` length 1..260; `MimeType` whitelist
  (`video/mp4`, `video/webm`, `application/pdf`, `image/jpeg`, `image/png`).
- `SizeBytes`: > 0, ≤ 5 GiB for video, ≤ 100 MiB for pdf/image.
- Multipart upload limits configured via Kestrel `MaxRequestBodySize` and
  `FormOptions.MultipartBodyLengthLimit` to match max video size.

### Error shape
RFC7807 `ProblemDetails`. `404` for cross-tenant lookups (never `403` — leak-safe).

---

## 7. Worker processing flow

1. Worker consumes `ContentUploaded`.
2. Loads `ContentItem` (with `tenantId` predicate). If `Status != Uploaded`
   or `IsDeleted` → ack and exit (idempotent).
3. Sets `Status = Processing`, `UpdatedAt = now`.
4. Phase 1 stub: synthesises HLS manifest key + thumbnail key + duration
   (no real ffmpeg). Real transcoding deferred — interface
   `IVideoProcessor.ProcessAsync(ContentItem, ct)` returns
   `ProcessingResult(string HlsManifestKey, string ThumbnailKey, int DurationSeconds)`.
5. On success: update doc (`Status=Ready`, keys, duration) and outbox-publish
   `ContentProcessingCompleted` (with `HlsManifestUrl` = SAS download URI
   for the manifest, TTL 60 min).
6. On failure: `Status=Failed`, `FailureReason`, publish
   `ContentProcessingFailed`.
7. Retry policy: MassTransit `UseMessageRetry(r => r.Intervals(1s, 5s, 30s))`
   then poison.

---

## 8. AppHost wiring (locked diff)

In `src/LMS.AppHost/Program.cs`:

```csharp
var contentDb = mongo.AddDatabase("lms-content");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();   // Azurite in dev
var contentBlobs = storage.AddBlobs("content-blobs");

var content = builder.AddProject<Projects.LMS_ContentService_Api>("content")
    .WithReference(contentDb)
    .WithReference(rabbitmq)
    .WithReference(contentBlobs)
    .WaitFor(contentDb)
    .WaitFor(contentBlobs)
    .WaitFor(rabbitmq);

var contentWorker = builder.AddProject<Projects.LMS_ContentService_Worker>("content-worker")
    .WithReference(contentDb)
    .WithReference(rabbitmq)
    .WithReference(contentBlobs)
    .WaitFor(content);

gateway.WithReference(content);
```

Gateway YARP route `/api/content/{**rest}` already configured in
`src/gateway/LMS.Gateway/appsettings.json`. Verify the gateway's
`MaxRequestBodySize` accommodates large multipart uploads (≤ 5 GiB).
If not, raise it (separate task in T7 spec).

---

## 9. Configuration keys

```
ConnectionStrings:lms-content              (Aspire-injected MongoDB connection string)
ConnectionStrings:rabbitmq                 (Aspire-injected)
ConnectionStrings:content-blobs            (Aspire-injected Azure Blob connection — Azurite in dev)
AzureBlob:Container                        lms-content
AzureBlob:DownloadUrlTtlMinutes            60
Content:MaxVideoSizeBytes                  5368709120
Content:MaxDocumentSizeBytes               104857600
```

Aspire's `AddAzureBlobClient("content-blobs")` reads
`ConnectionStrings:content-blobs` automatically.

---

## 10. Tests (locked surface)

Unit (in Api/Domain test projects):
- Storage key builder produces tenant-prefixed paths; rejects cross-tenant.
- Validators (size, mime).
- Worker processor stub idempotency (re-handle of same `EventId` no-op).

Integration (`LMS.IntegrationTests/ContentService/`) — Testcontainers
Mongo + Azurite (`mcr.microsoft.com/azure-storage/azurite`) + RabbitMQ:
- Tenant isolation pair: tenant A cannot GET/DELETE tenant B's item (404).
- Multipart upload to `POST /api/content/uploads` persists blob under
  tenant-prefixed path and returns 201 `ContentItemDto`.
- Upload publishes `ContentUploaded` (MassTransit harness).
- Worker consumes `ContentUploaded` → publishes `ContentProcessingCompleted`,
  doc Status flips Pending → Uploaded → Processing → Ready.
- `GET /{id}/playback` returns SAS URI only when `Status=Ready`.
- Soft delete: deleted item returns 404 on subsequent GET.

Architecture (`LMS.ArchitectureTests`):
- Every document in `LMS.ContentService.Domain.Documents` implements
  `ITenantDocument`.
- Every method on every `I*Repository` interface accepts a `Guid tenantId`
  parameter (reflection check).

Contract (`LMS.ContractTests`):
- `ContentUploaded`, `ContentProcessingCompleted`, `ContentProcessingFailed`
  shape stable.

---

## 11. Open decisions (auto-resolved — flagged for human review in summary)

1. **Worker collocated vs separate project**: chose **separate Worker project**
   to mirror the 4-project pattern (Migrator slot replaced by Worker).
2. **Object storage**: switched to **Azure Blob Storage** via
   `Aspire.Hosting.Azure.AddAzureStorage("storage").RunAsEmulator()`
   (Azurite in dev). Replaces MinIO; native Aspire integration removes
   the bespoke container wiring.
3. **Upload flow**: Phase 1 uses **direct multipart POST through the Api**
   (no presigned client-side upload). All bytes traverse the gateway → Api
   → Blob Storage. Presigned/SAS uploads can be added Phase 2 if size
   pressure demands it.
4. **`ContentUploaded` event** is NEW. Added to `LMS.Contracts.Content`.
   `docs/events.md` will gain this record in T11.
5. **`ContentType` enum lives in both Domain and Contracts** with identical
   ordinal values. Architecture test asserts equality.
6. **Soft delete only** in Phase 1 — physical blob cleanup deferred.
