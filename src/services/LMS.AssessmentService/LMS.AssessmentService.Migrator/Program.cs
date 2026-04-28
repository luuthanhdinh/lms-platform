using LMS.AssessmentService.Domain.Abstractions;
using LMS.AssessmentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ITenantContext>(new MigrationTenantContext());
builder.AddNpgsqlDataSource("lms-assessment");
builder.Services.AddDbContext<AssessmentDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "public")
                  .MigrationsAssembly("LMS.AssessmentService.Infrastructure"))
     .UseSnakeCaseNamingConvention());

var host = builder.Build();
await host.StartAsync();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AssessmentDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Applying AssessmentService migrations...");
await db.Database.MigrateAsync();
logger.LogInformation("Migrations applied successfully.");

await host.StopAsync();

internal sealed class MigrationTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string[] Roles => [];
}
