using MassTransit;

namespace LMS.NotificationWorker.Consumers;

public sealed class AccountDeactivatedConsumerDefinition : ConsumerDefinition<AccountDeactivatedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AccountDeactivatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(9)));
    }
}
