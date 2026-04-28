using LMS.NotificationWorker.Cache;
using LMS.NotificationWorker.Consumers;
using LMS.NotificationWorker.Email;
using LMS.NotificationWorker.Idempotency;
using LMS.NotificationWorker.Options;
using LMS.NotificationWorker.Templates;
using MassTransit;
using Microsoft.FeatureManagement;

namespace LMS.NotificationWorker.Extensions;

public static class WorkerExtensions
{
    public static IHostApplicationBuilder AddNotificationWorkerServices(this IHostApplicationBuilder builder)
    {
        // SMTP options
        builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

        // Notification options
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));

        // Core services
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.AddSingleton<IEmailTemplateRenderer, ScribanEmailTemplateRenderer>();
        builder.Services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();
        builder.Services.AddSingleton<IContactCache, RedisContactCache>();

        // Aspire Redis
        builder.AddRedisClient("redis");

        // Feature management
        builder.Services.AddFeatureManagement();

        // MassTransit with RabbitMQ
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<WelcomeEmailConsumer, WelcomeEmailConsumerDefinition>();
            x.AddConsumer<AccountDeactivatedConsumer, AccountDeactivatedConsumerDefinition>();
            x.AddConsumer<CoursePublishedConsumer, CoursePublishedConsumerDefinition>();
            x.AddConsumer<CourseArchivedConsumer, CourseArchivedConsumerDefinition>();
            x.AddConsumer<EnrollmentConfirmedConsumer, EnrollmentConfirmedConsumerDefinition>();
            x.AddConsumer<EnrollmentCancelledConsumer, EnrollmentCancelledConsumerDefinition>();
            x.AddConsumer<LessonCompletedConsumer, LessonCompletedConsumerDefinition>();
            x.AddConsumer<CourseCompletedConsumer, CourseCompletedConsumerDefinition>();
            x.AddConsumer<ContentProcessingCompletedConsumer, ContentProcessingCompletedConsumerDefinition>();
            x.AddConsumer<ContentProcessingFailedConsumer, ContentProcessingFailedConsumerDefinition>();
            x.AddConsumer<AssessmentResultConsumer, AssessmentResultConsumerDefinition>();
            x.AddConsumer<CertificateIssuedConsumer, CertificateIssuedConsumerDefinition>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return builder;
    }
}
