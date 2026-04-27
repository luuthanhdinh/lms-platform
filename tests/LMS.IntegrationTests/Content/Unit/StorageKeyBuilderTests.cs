using LMS.ContentService.Domain.Storage;
using Xunit;

namespace LMS.IntegrationTests.Content.Unit;

[Trait("Category", "Content")]
public class StorageKeyBuilderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ContentItemId = Guid.NewGuid();

    [Fact]
    public void Original_ReturnsCorrectPath()
    {
        var key = StorageKeyBuilder.Original(TenantId, ContentItemId, "video.mp4");

        Assert.Equal($"{TenantId}/{ContentItemId}/original/video.mp4", key);
    }

    [Fact]
    public void HlsManifest_ReturnsCorrectPath()
    {
        var key = StorageKeyBuilder.HlsManifest(TenantId, ContentItemId);

        Assert.Equal($"{TenantId}/{ContentItemId}/hls/master.m3u8", key);
    }

    [Fact]
    public void Thumbnail_ReturnsCorrectPath()
    {
        var key = StorageKeyBuilder.Thumbnail(TenantId, ContentItemId);

        Assert.Equal($"{TenantId}/{ContentItemId}/thumbs/poster.jpg", key);
    }

    [Fact]
    public void EnsureTenantScope_ValidKey_DoesNotThrow()
    {
        var key = StorageKeyBuilder.Original(TenantId, ContentItemId, "file.mp4");

        var ex = Record.Exception(() => StorageKeyBuilder.EnsureTenantScope(TenantId, ContentItemId, key));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureTenantScope_WrongTenant_Throws()
    {
        var wrongTenantId = Guid.NewGuid();
        // key belongs to TenantId, but we check with wrongTenantId
        var key = StorageKeyBuilder.Original(TenantId, ContentItemId, "file.mp4");

        Assert.Throws<UnauthorizedAccessException>(
            () => StorageKeyBuilder.EnsureTenantScope(wrongTenantId, ContentItemId, key));
    }

    [Fact]
    public void EnsureTenantScope_WrongContentItem_Throws()
    {
        var wrongContentItemId = Guid.NewGuid();
        // key belongs to ContentItemId, but we check with wrongContentItemId
        var key = StorageKeyBuilder.Original(TenantId, ContentItemId, "file.mp4");

        Assert.Throws<UnauthorizedAccessException>(
            () => StorageKeyBuilder.EnsureTenantScope(TenantId, wrongContentItemId, key));
    }
}
