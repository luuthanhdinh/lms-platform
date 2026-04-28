using LMS.CertificateService.Domain.Interfaces;
using LMS.CertificateService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Migrator needs a design-time tenant context (no HTTP context)
builder.Services.AddSingleton<ITenantContext>(new MigratorTenantContext());

builder.Services.AddDbContext<CertificateDbContext>(options =>
{
    var connectionString = builder.Configuration["ConnectionStrings:certificatedb"]
        ?? builder.Configuration["ConnectionStrings:lms-certificate"];
    options.UseNpgsql(connectionString);
});

var host = builder.Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
await db.Database.MigrateAsync();

// Worker exits after migration completes (WaitForCompletion in AppHost)

internal sealed class MigratorTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
}
