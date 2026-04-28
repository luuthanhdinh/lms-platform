using LMS.Contracts.Course;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;
using Microsoft.Extensions.Options;

namespace LMS.NotificationWorker.Consumers;

public sealed class CoursePublishedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<CoursePublishedConsumer> logger)
    : IConsumer<CoursePublished>
{
    public async Task Consume(ConsumeContext<CoursePublished> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"course-published:{msg.TenantId}:{msg.CourseId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Course published email already sent for course {CourseId}", msg.CourseId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.InstructorId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for instructor {InstructorId} in tenant {TenantId}; dropping course-published email",
                msg.InstructorId, msg.TenantId);
            return;
        }

        var courseUrl = $"{options.Value.FrontendBaseUrl}/courses/{msg.CourseId}";
        var model = new CoursePublishedViewModel(
            CourseName: $"Course {msg.CourseId}",
            CourseUrl: courseUrl);

        var (subject, html, text) = await templateRenderer.RenderAsync("course-published", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Course published email sent to instructor {InstructorId} for course {CourseId}",
            msg.InstructorId, msg.CourseId);
    }
}
