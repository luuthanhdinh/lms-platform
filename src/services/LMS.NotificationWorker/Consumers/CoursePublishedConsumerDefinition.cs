using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class CoursePublishedConsumerDefinition : ConsumerDefinition<CoursePublishedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<CoursePublishedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(9)));
    }
}
