using LMS.ContentService.Domain.Documents;
using LMS.ContentService.Domain.Models;

namespace LMS.ContentService.Api.Mapping;

public static class ContentMappingExtensions
{
    public static ContentItemDto ToDto(this ContentItem item,
        string? hlsManifestUrl = null, string? thumbnailUrl = null) => new(
        item.Id, item.TenantId, item.UploadedBy,
        item.OriginalFileName, item.MimeType,
        item.Type, item.Status,
        item.SizeBytes, item.DurationSeconds,
        hlsManifestUrl, thumbnailUrl,
        item.FailureReason,
        item.CreatedAt, item.UpdatedAt);
}
