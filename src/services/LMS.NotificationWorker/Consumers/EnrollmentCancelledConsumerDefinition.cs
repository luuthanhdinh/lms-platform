using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class EnrollmentCancelledConsumerDefinition : ConsumerDefinition<EnrollmentCancelledConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<EnrollmentCancelledConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(9)));
    }
}
