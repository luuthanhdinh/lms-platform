using LMS.IdentityService.Api.Auth;
using LMS.IdentityService.Api.Endpoints;
using LMS.IdentityService.Domain.Abstractions;
using LMS.IdentityService.Infrastructure;
using LMS.IdentityService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// EF Core via Aspire
builder.AddNpgsqlDataSource("lms-identity");
builder.Services.AddDbContext<IdentityDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

// Infrastructure repositories
builder.Services.AddIdentityInfrastructure();

// Tenant context (reads X-* headers)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();

// MassTransit + RabbitMQ + outbox
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<IdentityDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost");
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

// Map all endpoint groups
app.MapProfileEndpoints();
app.MapUserAdminEndpoints();
app.MapTenantEndpoints();

app.Run();
