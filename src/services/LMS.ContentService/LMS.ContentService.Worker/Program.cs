using LMS.ContentService.Infrastructure.Extensions;
using LMS.ContentService.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.AddAzureBlobClient("content-blobs");

builder.Services.AddContentInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ContentUploadedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.ConfigureEndpoints(ctx);
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30)));
    });
});

var host = builder.Build();
await host.RunAsync();
