using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class ContentProcessingCompletedConsumerDefinition : ConsumerDefinition<ContentProcessingCompletedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ContentProcessingCompletedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(9)));
    }
}
