using LMS.ProgressService.Api.Endpoints;
using LMS.ProgressService.Domain.Abstractions;
using LMS.ProgressService.Infrastructure.Auth;
using LMS.ProgressService.Infrastructure.Consumers;
using LMS.ProgressService.Infrastructure.Data;
using LMS.ProgressService.Infrastructure.Extensions;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddProgressInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserEnrolledConsumer>();
    x.AddConsumer<EnrollmentCancelledConsumer>();

    x.AddEntityFrameworkOutbox<ProgressDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.UseMessageRetry(r => r.Intervals(1000, 5000, 30000));
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapProgressEndpoints();
app.Run();
