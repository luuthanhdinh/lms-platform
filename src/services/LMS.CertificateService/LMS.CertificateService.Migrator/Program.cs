using LMS.CertificateService.Domain.Interfaces;
using LMS.CertificateService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ITenantContext>(new MigratorTenantContext());
builder.AddNpgsqlDataSource("lms-certificate");
builder.Services.AddDbContext<CertificateDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "public")
                  .MigrationsAssembly("LMS.CertificateService.Infrastructure"))
     .UseSnakeCaseNamingConvention());

var host = builder.Build();
await host.StartAsync();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Applying CertificateService migrations...");
await db.Database.MigrateAsync();
logger.LogInformation("Migrations applied successfully.");

await host.StopAsync();

internal sealed class MigratorTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
}
