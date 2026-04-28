using LMS.Contracts.Assessment;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;
using Microsoft.Extensions.Options;

namespace LMS.NotificationWorker.Consumers;

public sealed class AssessmentResultConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<AssessmentResultConsumer> logger)
    : IConsumer<AssessmentSubmitted>
{
    public async Task Consume(ConsumeContext<AssessmentSubmitted> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"assessment-result:{msg.TenantId}:{msg.UserId}:{msg.AssessmentId}:{msg.OccurredAt:yyyyMMddHHmm}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Assessment result email already sent for user {UserId} assessment {AssessmentId}",
                msg.UserId, msg.AssessmentId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping assessment-result email",
                msg.UserId, msg.TenantId);
            return;
        }

        var retakeUrl = msg.Passed
            ? null
            : $"{options.Value.FrontendBaseUrl}/courses/{msg.CourseId}/assessments/{msg.AssessmentId}";

        var model = new AssessmentResultViewModel(
            AssessmentName: $"Assessment {msg.AssessmentId}",
            Score: msg.Score,
            Passed: msg.Passed,
            RetakeUrl: retakeUrl);

        var (subject, html, text) = await templateRenderer.RenderAsync("assessment-result", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Assessment result email sent for user {UserId} assessment {AssessmentId} score {Score} in tenant {TenantId}",
            msg.UserId, msg.AssessmentId, msg.Score, msg.TenantId);
    }
}
