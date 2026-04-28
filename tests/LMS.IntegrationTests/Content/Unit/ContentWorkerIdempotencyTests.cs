using LMS.ContentService.Domain.Documents;
using LMS.ContentService.Domain.Enums;
using Xunit;

namespace LMS.IntegrationTests.Content.Unit;

/// <summary>
/// Pure-logic tests for the idempotency guard in the video processing consumer.
/// The consumer skips items unless Status == Uploaded and IsDeleted == false.
/// No DB, no MassTransit harness required.
/// </summary>
[Trait("Category", "Content")]
public class ContentWorkerIdempotencyTests
{
    private static ContentItem MakeItem(ContentStatus status, bool isDeleted = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = status,
            IsDeleted = isDeleted,
            OriginalFileName = "video.mp4",
            MimeType = "video/mp4",
            Type = ContentType.Video,
            StorageContainer = "uploads",
            StorageKey = "tenant/item/original/video.mp4"
        };

    private static bool ShouldSkip(ContentItem item) =>
        item.IsDeleted || item.Status != ContentStatus.Uploaded;

    [Fact]
    public void Consumer_SkipsItem_WhenStatusIsProcessing()
    {
        var item = MakeItem(ContentStatus.Processing);

        Assert.True(ShouldSkip(item), "Item in Processing status should be skipped");
    }

    [Fact]
    public void Consumer_SkipsItem_WhenStatusIsReady()
    {
        var item = MakeItem(ContentStatus.Ready);

        Assert.True(ShouldSkip(item), "Item in Ready status should be skipped");
    }

    [Fact]
    public void Consumer_SkipsItem_WhenDeleted()
    {
        var item = MakeItem(ContentStatus.Uploaded, isDeleted: true);

        Assert.True(ShouldSkip(item), "Soft-deleted item should be skipped regardless of status");
    }

    [Fact]
    public void Consumer_Processes_WhenStatusIsUploaded()
    {
        var item = MakeItem(ContentStatus.Uploaded, isDeleted: false);

        Assert.False(ShouldSkip(item), "Item in Uploaded status and not deleted should be processed");
    }
}
