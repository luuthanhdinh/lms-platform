namespace LMS.Contracts.Content;

/// <summary>
/// Published by ContentService.Api when a file is successfully stored in blob storage.
/// Consumers: ContentService.Worker (trigger async processing pipeline).
/// </summary>
public sealed record ContentUploaded(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Surrogate key of the content item that was just stored in blob storage.</summary>
    Guid ContentItemId,

    /// <summary>Tenant that owns the content. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Surrogate key of the user (instructor or admin) who performed the upload.</summary>
    Guid UploadedBy,

    /// <summary>Kind of asset that was uploaded — determines the processing pipeline chosen by the worker.</summary>
    ContentType Type,

    /// <summary>Raw file size in bytes as reported by the storage backend after the write completed.</summary>
    long SizeBytes,

    /// <summary>UTC instant the blob write was confirmed by the storage backend.</summary>
    DateTimeOffset OccurredAt);
