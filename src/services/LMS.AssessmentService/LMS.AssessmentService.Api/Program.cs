using LMS.AssessmentService.Api.Endpoints;
using LMS.AssessmentService.Domain.Abstractions;
using LMS.AssessmentService.Infrastructure.Auth;
using LMS.AssessmentService.Infrastructure.Consumers;
using LMS.AssessmentService.Infrastructure.Data;
using LMS.AssessmentService.Infrastructure.Extensions;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("lms-assessment");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddAssessmentInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CourseArchivedConsumer>();

    x.AddEntityFrameworkOutbox<AssessmentDbContext>(o =>
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
app.MapAssessmentEndpoints();
app.Run();
