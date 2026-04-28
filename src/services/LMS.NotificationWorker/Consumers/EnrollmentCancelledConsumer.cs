using LMS.Contracts.Enrollment;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class EnrollmentCancelledConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    ILogger<EnrollmentCancelledConsumer> logger)
    : IConsumer<EnrollmentCancelled>
{
    public async Task Consume(ConsumeContext<EnrollmentCancelled> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"cancelled:{msg.TenantId}:{msg.UserId}:{msg.CourseId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Enrollment cancelled email already sent for user {UserId} course {CourseId}",
                msg.UserId, msg.CourseId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping enrollment-cancelled email",
                msg.UserId, msg.TenantId);
            return;
        }

        var model = new EnrollmentCancelledViewModel(
            CourseName: $"Course {msg.CourseId}");

        var (subject, html, text) = await templateRenderer.RenderAsync("enrollment-cancelled", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Enrollment cancelled email sent for user {UserId} course {CourseId} in tenant {TenantId}",
            msg.UserId, msg.CourseId, msg.TenantId);
    }
}
