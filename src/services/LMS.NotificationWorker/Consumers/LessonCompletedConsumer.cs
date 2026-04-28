using LMS.Contracts.Progress;
using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Idempotency;
using MassTransit;
using Microsoft.FeatureManagement;

namespace LMS.NotificationWorker.Consumers;

public sealed class LessonCompletedConsumer(
    IIdempotencyService idempotency,
    IContactCache contactCache,
    IFeatureManager featureManager,
    ILogger<LessonCompletedConsumer> logger)
    : IConsumer<LessonCompleted>
{
    private const string FeatureFlag = "Notifications.LessonProgressEmail";

    public async Task Consume(ConsumeContext<LessonCompleted> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (!await featureManager.IsEnabledAsync(FeatureFlag))
        {
            return;
        }

        var key = $"lesson-completed:{msg.TenantId}:{msg.UserId}:{msg.LessonId}";
        if (!await idempotency.TryClaimAsync(key, TimeSpan.FromDays(7), ct))
        {
            logger.LogInformation("Lesson completed email already sent for user {UserId} lesson {LessonId}",
                msg.UserId, msg.LessonId);
            return;
        }

        var contact = await contactCache.GetAsync(msg.TenantId, msg.UserId, ct);
        if (contact is null)
        {
            logger.LogWarning("Contact not found for user {UserId} in tenant {TenantId}; dropping lesson-completed email",
                msg.UserId, msg.TenantId);
            return;
        }

        // Feature is gated — progress email is a Phase 3 stub; log and no-op for now
        logger.LogInformation("Lesson completed notification acknowledged for user {UserId} lesson {LessonId} (email send deferred to Phase 3)",
            msg.UserId, msg.LessonId);
    }
}
