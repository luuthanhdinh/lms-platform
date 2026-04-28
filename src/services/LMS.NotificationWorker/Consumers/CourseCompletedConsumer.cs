using LMS.Contracts.Progress;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class CourseCompletedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    ILogger<CourseCompletedConsumer> logger)
    : IConsumer<CourseCompleted>
{
    public async Task Consume(ConsumeContext<CourseCompleted> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"course-completed:{msg.TenantId}:{msg.UserId}:{msg.CourseId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Course completed email already sent for user {UserId} course {CourseId}",
                msg.UserId, msg.CourseId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping course-completed email",
                msg.UserId, msg.TenantId);
            return;
        }

        var model = new CourseCompletedViewModel(
            CourseName: msg.CourseName ?? $"Course {msg.CourseId}",
            FullName: msg.LearnerName ?? contact.FullName,
            CertificateIssued: false);

        var (subject, html, text) = await templateRenderer.RenderAsync("course-completed", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Course completed email sent for user {UserId} course {CourseId} in tenant {TenantId}",
            msg.UserId, msg.CourseId, msg.TenantId);
    }
}
