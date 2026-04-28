namespace LMS.Contracts.Content;

/// <summary>
/// Published by ContentService when video/media transcoding has finished and the HLS manifest is ready.
/// Consumers: CourseService (mark content item as ready), NotificationWorker (instructor upload notice).
/// </summary>
public sealed record ContentProcessingCompleted(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Surrogate key of the content item whose processing completed.</summary>
    Guid ContentItemId,

    /// <summary>Tenant that owns the content. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Publicly accessible URL of the HLS master manifest (.m3u8).</summary>
    string HlsManifestUrl,

    /// <summary>Total duration of the processed media in seconds.</summary>
    int DurationSeconds,

    /// <summary>UTC instant processing was completed and the manifest became available.</summary>
    DateTimeOffset OccurredAt,

    /// <summary>UserId of the person who uploaded the content.</summary>
    Guid UploadedBy);
