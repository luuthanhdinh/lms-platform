using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Domain.Enums;
using LMS.ContentService.Domain.Repositories;
using LMS.Contracts.Content;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace LMS.ContentService.Worker.Consumers;

public sealed class ContentUploadedConsumer : IConsumer<ContentUploaded>
{
    private readonly IContentItemRepository _repo;
    private readonly IVideoProcessor _processor;
    private readonly IContentStorageService _storage;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<ContentUploadedConsumer> _logger;

    public ContentUploadedConsumer(
        IContentItemRepository repo,
        IVideoProcessor processor,
        IContentStorageService storage,
        IPublishEndpoint bus,
        ILogger<ContentUploadedConsumer> logger)
    {
        _repo = repo;
        _processor = processor;
        _storage = storage;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ContentUploaded> context)
    {
        var ct = context.CancellationToken;
        var msg = context.Message;

        var item = await _repo.FindByIdAsync(msg.TenantId, msg.ContentItemId, ct);
        if (item is null)
        {
            _logger.LogWarning("ContentItem {Id} not found for tenant {TenantId}", msg.ContentItemId, msg.TenantId);
            return;
        }

        // Idempotency: skip if already processed or soft-deleted
        if (item.Status != ContentStatus.Uploaded || item.IsDeleted)
        {
            _logger.LogInformation("Skipping {Id} — status={Status}, deleted={Deleted}",
                item.Id, item.Status, item.IsDeleted);
            return;
        }

        item.Status = ContentStatus.Processing;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(item, ct);

        try
        {
            var result = await _processor.ProcessAsync(item, ct);

            item.Status = ContentStatus.Ready;
            item.HlsManifestKey = result.HlsManifestKey;
            item.ThumbnailKey = result.ThumbnailKey;
            item.DurationSeconds = result.DurationSeconds;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await _repo.UpdateAsync(item, ct);

            var hlsUrl = await _storage.GetDownloadUriAsync(
                item.TenantId, item.Id, result.HlsManifestKey,
                TimeSpan.FromMinutes(60), ct);

            await _bus.Publish(new ContentProcessingCompleted(
                Guid.NewGuid(), item.Id, item.TenantId,
                hlsUrl.ToString(), result.DurationSeconds,
                DateTimeOffset.UtcNow, msg.UploadedBy), ct);

            _logger.LogInformation("Processing complete for {Id}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for {Id}", item.Id);

            item.Status = ContentStatus.Failed;
            item.FailureReason = ex.Message;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await _repo.UpdateAsync(item, ct);

            await _bus.Publish(new ContentProcessingFailed(
                Guid.NewGuid(), item.Id, item.TenantId,
                ex.Message, DateTimeOffset.UtcNow, msg.UploadedBy), ct);

            throw; // let MassTransit retry policy fire
        }
    }
}
