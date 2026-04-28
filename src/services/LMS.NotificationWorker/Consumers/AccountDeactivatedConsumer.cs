using LMS.Contracts.Identity;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;
using Microsoft.Extensions.Options;

namespace LMS.NotificationWorker.Consumers;

public sealed class AccountDeactivatedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<AccountDeactivatedConsumer> logger)
    : IConsumer<UserDeactivated>
{
    public async Task Consume(ConsumeContext<UserDeactivated> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"deactivated:{msg.TenantId}:{msg.UserId}:{msg.OccurredAt:yyyyMMddHHmm}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Account deactivated email already sent for user {UserId}", msg.UserId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping account-deactivated email",
                msg.UserId, msg.TenantId);
            return;
        }

        var model = new AccountDeactivatedViewModel(
            PlatformName: options.Value.PlatformName,
            FullName: contact.FullName);

        var (subject, html, text) = await templateRenderer.RenderAsync("account-deactivated", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Account deactivated email sent for user {UserId} in tenant {TenantId}", msg.UserId, msg.TenantId);
    }
}
