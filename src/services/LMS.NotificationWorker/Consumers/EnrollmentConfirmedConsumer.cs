using LMS.Contracts.Enrollment;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;
using Microsoft.Extensions.Options;

namespace LMS.NotificationWorker.Consumers;

public sealed class EnrollmentConfirmedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<EnrollmentConfirmedConsumer> logger)
    : IConsumer<UserEnrolled>
{
    public async Task Consume(ConsumeContext<UserEnrolled> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"enrolled:{msg.TenantId}:{msg.UserId}:{msg.CourseId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Enrollment confirmed email already sent for user {UserId} course {CourseId}",
                msg.UserId, msg.CourseId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping enrollment-confirmed email",
                msg.UserId, msg.TenantId);
            return;
        }

        var courseUrl = $"{options.Value.FrontendBaseUrl}/courses/{msg.CourseId}";
        var model = new EnrollmentConfirmedViewModel(
            CourseName: $"Course {msg.CourseId}",
            CourseUrl: courseUrl);

        var (subject, html, text) = await templateRenderer.RenderAsync("enrollment-confirmed", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Enrollment confirmed email sent for user {UserId} course {CourseId} in tenant {TenantId}",
            msg.UserId, msg.CourseId, msg.TenantId);
    }
}
