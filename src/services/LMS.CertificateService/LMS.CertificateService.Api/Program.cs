using LMS.CertificateService.Api.Endpoints;
using LMS.CertificateService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCertificateInfrastructure();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapCertificateEndpoints();

app.Run();
