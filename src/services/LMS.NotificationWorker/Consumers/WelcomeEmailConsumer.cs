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

public sealed class WelcomeEmailConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<WelcomeEmailConsumer> logger)
    : IConsumer<UserRegistered>
{
    public async Task Consume(ConsumeContext<UserRegistered> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        // Populate cache first so future consumers can resolve this user
        await contactCache.SetAsync(msg.TenantId, msg.UserId,
            new ContactInfo(msg.Email, msg.DisplayName), ct);

        var key = $"welcome:{msg.TenantId}:{msg.UserId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Welcome email already sent for user {UserId}", msg.UserId);
            return;
        }

        var model = new WelcomeViewModel(
            PlatformName: options.Value.PlatformName,
            FullName: msg.DisplayName,
            LoginUrl: $"{options.Value.FrontendBaseUrl}/login");

        var (subject, html, text) = await templateRenderer.RenderAsync("welcome", model, ct);
        await emailSender.SendAsync(new EmailMessage(msg.Email, subject, html, text), ct);

        logger.LogInformation("Welcome email sent for user {UserId} in tenant {TenantId}", msg.UserId, msg.TenantId);
    }
}
