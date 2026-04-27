using LMS.ContentService.Domain.Enums;

namespace LMS.ContentService.Domain.Models;

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

public record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long Total);
