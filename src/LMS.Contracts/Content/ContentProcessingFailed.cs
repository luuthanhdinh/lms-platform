namespace LMS.Contracts.Content;

/// <summary>
/// Published by ContentService.Worker when processing fails after all retries are exhausted.
/// Consumers: NotificationWorker (alert instructor of failure), CourseService (mark content item as failed).
/// </summary>
public sealed record ContentProcessingFailed(
    /// <summary>Unique event identifier used for idempotency (MassTransit inbox key).</summary>
    Guid EventId,

    /// <summary>Surrogate key of the content item whose processing pipeline failed.</summary>
    Guid ContentItemId,

    /// <summary>Tenant that owns the content. Must match the X-Tenant-Id message header.</summary>
    Guid TenantId,

    /// <summary>Human-readable description of the terminal failure captured from the last retry attempt.</summary>
    string Reason,

    /// <summary>UTC instant the worker gave up after exhausting all configured redelivery intervals.</summary>
    DateTimeOffset OccurredAt,

    /// <summary>UserId of the person who uploaded the content.</summary>
    Guid UploadedBy);
