using LMS.Contracts.Course;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class CourseArchivedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    ILogger<CourseArchivedConsumer> logger)
    : IConsumer<CourseArchived>
{
    public async Task Consume(ConsumeContext<CourseArchived> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"course-archived:{msg.TenantId}:{msg.CourseId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Course archived email already sent for course {CourseId}", msg.CourseId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.InstructorId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for instructor {InstructorId} in tenant {TenantId}; dropping course-archived email",
                msg.InstructorId, msg.TenantId);
            return;
        }

        var model = new CourseArchivedViewModel(
            CourseName: $"Course {msg.CourseId}");

        var (subject, html, text) = await templateRenderer.RenderAsync("course-archived", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Course archived email sent to instructor {InstructorId} for course {CourseId}",
            msg.InstructorId, msg.CourseId);
    }
}
