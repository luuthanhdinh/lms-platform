using LMS.ContentService.Api.Auth;
using LMS.ContentService.Api.Endpoints;
using LMS.ContentService.Domain.Abstractions;
using LMS.ContentService.Infrastructure.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Aspire-injected Azure Blob client
builder.AddAzureBlobServiceClient("content-blobs");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddContentInfrastructure(builder.Configuration);

// Api uses publish-only bus (no consumers)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.UseMessageRetry(r => r.Intervals(500, 1000, 2000));
    });
});

// Allow large multipart uploads (up to 5 GiB for video)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5L * 1024 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 5L * 1024 * 1024 * 1024;
});

var app = builder.Build();
app.MapDefaultEndpoints();

// Ensure MongoDB indexes exist
await app.Services.EnsureContentIndexesAsync();

app.MapContentEndpoints();
app.Run();
