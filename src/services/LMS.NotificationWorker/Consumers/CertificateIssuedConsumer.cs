using LMS.Contracts.Certificate;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using LMS.NotificationWorker.Templates.ViewModels;
using MassTransit;
using Microsoft.Extensions.Options;

namespace LMS.NotificationWorker.Consumers;

public sealed class CertificateIssuedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IEmailTemplateRenderer templateRenderer,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<CertificateIssuedConsumer> logger)
    : IConsumer<CertificateIssued>
{
    public async Task Consume(ConsumeContext<CertificateIssued> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var key = $"cert-issued:{msg.TenantId}:{msg.CertificateId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Certificate issued email already sent for certificate {CertificateId}", msg.CertificateId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping certificate-issued email",
                msg.UserId, msg.TenantId);
            return;
        }

        var verificationUrl = $"{options.Value.VerifyBaseUrl}/{msg.VerificationCode}";
        var model = new CertificateIssuedViewModel(
            CourseName: $"Course {msg.CourseId}",
            FullName: contact.FullName,
            CertificateNumber: msg.CertificateNumber,
            VerificationUrl: verificationUrl);

        var (subject, html, text) = await templateRenderer.RenderAsync("certificate-issued", model, ct);
        await emailSender.SendAsync(new EmailMessage(contact.Email, subject, html, text), ct);

        logger.LogInformation("Certificate issued email sent for user {UserId} certificate {CertificateId} in tenant {TenantId}",
            msg.UserId, msg.CertificateId, msg.TenantId);
    }
}
