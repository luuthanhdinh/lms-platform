using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Domain.Documents;
using LMS.ContentService.Domain.Storage;

namespace LMS.ContentService.Infrastructure.Processors;

public sealed class StubVideoProcessor : IVideoProcessor
{
    public Task<ProcessingResult> ProcessAsync(ContentItem item, CancellationToken ct = default)
    {
        // Phase 1 stub: synthesise keys, duration = 0
        var result = new ProcessingResult(
            HlsManifestKey: StorageKeyBuilder.HlsManifest(item.TenantId, item.Id),
            ThumbnailKey: StorageKeyBuilder.Thumbnail(item.TenantId, item.Id),
            DurationSeconds: 0);
        return Task.FromResult(result);
    }
}
