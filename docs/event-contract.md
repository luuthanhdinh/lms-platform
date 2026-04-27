# Event Contract — Content Upload & Processing

## Events

### ContentUploaded
| Field | Type | Description |
|---|---|---|
| EventId | Guid | Idempotency key (MassTransit inbox) |
| ContentItemId | Guid | Surrogate key of the stored asset |
| TenantId | Guid | Owner tenant — must match X-Tenant-Id header |
| UploadedBy | Guid | Instructor or admin who performed the upload |
| Type | ContentType | Video / Pdf / Image / Other |
| SizeBytes | long | Raw byte count confirmed by blob storage |
| OccurredAt | DateTimeOffset | UTC instant blob write was confirmed |

### ContentProcessingCompleted
| Field | Type | Description |
|---|---|---|
| EventId | Guid | Idempotency key |
| ContentItemId | Guid | Surrogate key of the processed asset |
| TenantId | Guid | Owner tenant |
| HlsManifestUrl | string | Publicly accessible .m3u8 URL |
| DurationSeconds | int | Total media duration |
| OccurredAt | DateTimeOffset | UTC instant processing completed |

### ContentProcessingFailed
| Field | Type | Description |
|---|---|---|
| EventId | Guid | Idempotency key |
| ContentItemId | Guid | Surrogate key of the failed asset |
| TenantId | Guid | Owner tenant |
| Reason | string | Terminal failure message from last retry |
| OccurredAt | DateTimeOffset | UTC instant worker gave up |

## Publisher → Consumer mapping

| Event | Publisher | Consumers |
|---|---|---|
| ContentUploaded | ContentService.Api | ContentService.Worker |
| ContentProcessingCompleted | ContentService.Worker | CourseService, NotificationWorker |
| ContentProcessingFailed | ContentService.Worker | NotificationWorker, CourseService |

## Queue names (kebab-case convention)

| Consumer | Queue |
|---|---|
| ContentService.Worker / ContentUploaded | `content-uploaded` |
| CourseService / ContentProcessingCompleted | `course-content-processing-completed` |
| NotificationWorker / ContentProcessingCompleted | `notification-content-processing-completed` |
| CourseService / ContentProcessingFailed | `course-content-processing-failed` |
| NotificationWorker / ContentProcessingFailed | `notification-content-processing-failed` |

Poison queues follow the `<queue>_error` convention monitored by alerting.

## Ordering guarantees

No global ordering is required. Each `ContentItemId` is independent.
Within a single content item the sequence is always:
1. `ContentUploaded` (Api commits to DB + outbox)
2. `ContentProcessingCompleted` OR `ContentProcessingFailed` (Worker commits)

MassTransit does not guarantee delivery order across these two queues,
but the domain is safe: the Worker consumes `ContentUploaded` before it
can produce either terminal event.

## Retry / redelivery / poison configuration

```csharp
cfg.UseMessageRetry(r => r.Intervals(100, 500, 2000));
cfg.UseDelayedRedelivery(r => r.Intervals(
    TimeSpan.FromMinutes(1),
    TimeSpan.FromMinutes(15),
    TimeSpan.FromHours(1)));
// Terminal failures land in <queue>_error — alert on queue depth
```

## Outbox

`ContentService.Api` uses the MassTransit EF Core outbox on its
`ContentDbContext` so the blob-write acknowledgement and the
`ContentUploaded` publish are atomic. Configure with:

```csharp
x.AddEntityFrameworkOutbox<ContentDbContext>(o =>
{
    o.UsePostgres();
    o.UseBusOutbox();
});
```

## Idempotency

All consumers key on `EventId` via the MassTransit inbox. Additionally:

- ContentService.Worker checks `ContentItem.Status` before starting the
  pipeline — if already `Processing` or `Ready` it returns early.
- CourseService checks the `ContentItem` record status before updating.
- NotificationWorker checks an outbox record keyed on `EventId` before
  sending an email.

## Saga

No saga required — the flow is linear (2 steps) with no compensation.
If a third step (e.g., caption generation) is added the recommendation
is to introduce a `ContentProcessingSaga`.

## Expected throughput

Phase 1: < 50 uploads / hour per tenant, < 5 tenants.
No special partitioning needed. Default prefetch count (16) is sufficient.

## Failure modes

| Failure | Effect | Recovery |
|---|---|---|
| Blob storage unavailable during upload | Api returns 503; no event published | Client retries upload |
| Worker crashes mid-transcode | Message redelivered; worker re-starts transcode from scratch (idempotent) | Automatic via redelivery policy |
| All redeliveries exhausted | `ContentProcessingFailed` published; message dead-lettered to `content-uploaded_error` | Alert fires; manual re-trigger via admin API |
| Consumer down at time of publish | RabbitMQ holds the message until consumer reconnects | No data loss; SLA depends on broker availability |
