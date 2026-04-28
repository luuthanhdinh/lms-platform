# ContentService documentation has moved

See `docs/services/content.md` for current documentation.
Media asset management: upload, transcoding, streaming, download.
Metadata in MongoDB. Binaries in S3. Videos → HLS via FFmpeg Kubernetes Job.
Owns resume position (ADR-004). ProgressService owns completion %.

## Endpoints

```
POST /api/content/upload                   ← UploadRequest → UploadUrlResponse
GET  /api/content/{id}                     → ContentItem
DELETE /api/content/{id}
POST /api/content/{id}/process                                   # triggers transcoding
GET  /api/content/{id}/stream              → StreamUrlResponse   # enrollment required
POST /api/content/{id}/stream/refresh      → StreamUrlResponse
GET  /api/content/{id}/download            → DownloadUrlResponse # enrollment required
POST /api/content/{id}/progress            ← PlaybackProgressRequest  # every 30s
POST /api/content/{id}/stream/heartbeat                          # Phase 3 DRM
```

## Key flows

**Upload:**
1. `POST /upload` → service creates `ContentItem {Status=Pending}` → returns pre-signed S3 PUT URL
2. Client uploads directly to S3 (no server proxy)
3. `POST /{id}/process` → enqueue Kubernetes Job with FFmpeg

**Transcoding Job (ADR-003):**
- FFmpeg renders: 360p@500kbps · 720p@2500kbps · 1080p@5000kbps
- Output: HLS manifest + `.ts` segments in S3 under `hls/{contentItemId}/`
- Thumbnail extracted at 10% mark
- Job completes → HTTP callback to ContentService → `Status=Ready`
- Publish `ContentProcessingCompleted`

**Streaming:**
- Verify enrollment: `GET http://enrollment/api/enrollments/check?courseId=&userId=`
- Return signed CloudFront URL for HLS manifest (TTL 15 min)
- Include `resumePositionSeconds` from `PlaybackProgress` document

**Playback heartbeat:**
- Client calls every 30s
- Upsert `PlaybackProgress {positionSeconds}`
- Publish internal `PlaybackProgressReported` event → ProgressService auto-completes at 80%

## Events published
- `ContentProcessingCompleted`
- `ContentProcessingFailed`

## Events consumed
None — ContentService is primarily called via HTTP.
