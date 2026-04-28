using LMS.EnrollmentService.Api.Endpoints;
using LMS.EnrollmentService.Domain.Abstractions;
using LMS.EnrollmentService.Infrastructure.Auth;
using LMS.EnrollmentService.Infrastructure.Consumers;
using LMS.EnrollmentService.Infrastructure.Data;
using LMS.EnrollmentService.Infrastructure.Extensions;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddEnrollmentInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CourseArchivedConsumer>();

    x.AddEntityFrameworkOutbox<EnrollmentDbContext>(o =>
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
app.MapEnrollmentEndpoints();
app.Run();
