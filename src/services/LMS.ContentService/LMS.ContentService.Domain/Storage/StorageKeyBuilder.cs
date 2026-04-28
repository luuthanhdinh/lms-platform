namespace LMS.ContentService.Domain.Storage;

public static class StorageKeyBuilder
{
    public static string Original(Guid tenantId, Guid contentItemId, string fileName) =>
        $"{tenantId}/{contentItemId}/original/{fileName}";

    public static string HlsManifest(Guid tenantId, Guid contentItemId) =>
        $"{tenantId}/{contentItemId}/hls/master.m3u8";

    public static string Thumbnail(Guid tenantId, Guid contentItemId) =>
        $"{tenantId}/{contentItemId}/thumbs/poster.jpg";

    public static void EnsureTenantScope(Guid tenantId, Guid contentItemId, string storageKey)
    {
        var expectedPrefix = $"{tenantId}/{contentItemId}/";
        if (!storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                $"Storage key '{storageKey}' does not belong to tenant {tenantId} / content {contentItemId}.");
    }
}
