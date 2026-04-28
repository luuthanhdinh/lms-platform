using LMS.CourseService.Api.Auth;
using LMS.CourseService.Api.Endpoints;
using LMS.CourseService.Domain.Abstractions;
using LMS.CourseService.Infrastructure.Consumers;
using LMS.CourseService.Infrastructure.Data;
using LMS.CourseService.Infrastructure.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("lms-courses");
builder.Services.AddDbContext<CourseDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()).UseSnakeCaseNamingConvention());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddCourseInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<CourseDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
    x.AddConsumer<ContentProcessingCompletedConsumer>();
    x.AddConsumer<UserEnrolledConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.ConfigureEndpoints(ctx);
        cfg.UseMessageRetry(r => r.Intervals(500, 1000, 2000));
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapCourseEndpoints();
app.Run();
