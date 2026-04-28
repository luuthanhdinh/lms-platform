using LMS.ContentService.Domain.Enums;

namespace LMS.ContentService.Domain.Documents;

[BsonCollection("content_items")]
public class ContentItem : ContentDocument
{
    public Guid UploadedBy { get; set; }
    public string OriginalFileName { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public ContentType Type { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Pending;
    public long SizeBytes { get; set; }
    public string StorageContainer { get; set; } = default!;
    public string StorageKey { get; set; } = default!;
    public int? DurationSeconds { get; set; }
    public string? HlsManifestKey { get; set; }
    public string? ThumbnailKey { get; set; }
    public string? FailureReason { get; set; }
    public bool IsDeleted { get; set; }
}
