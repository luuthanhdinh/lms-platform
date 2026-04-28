using LMS.Contracts.Content;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class ContentProcessingFailedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    ILogger<ContentProcessingFailedConsumer> logger)
    : IConsumer<ContentProcessingFailed>
{
    public async Task Consume(ConsumeContext<ContentProcessingFailed> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"content-failed:{msg.TenantId}:{msg.ContentItemId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Content processing failed email already sent for content {ContentItemId}", msg.ContentItemId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UploadedBy, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for uploader {UploadedBy} in tenant {TenantId}; dropping content-processing-failed email",
                msg.UploadedBy, msg.TenantId);
            return;
        }

        var model = new ContentProcessingFailedViewModel(
            ContentTitle: $"Content {msg.ContentItemId}",
            Reason: msg.Reason);

        var (subject, html, text) = await templateRenderer.RenderAsync("content-processing-failed", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Content processing failed email sent for user {UserId} content {ContentItemId} in tenant {TenantId}",
            msg.UploadedBy, msg.ContentItemId, msg.TenantId);
    }
}
