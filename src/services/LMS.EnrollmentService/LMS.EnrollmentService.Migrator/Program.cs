using LMS.EnrollmentService.Domain.Abstractions;
using LMS.EnrollmentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ITenantContext>(new MigrationTenantContext());
builder.AddNpgsqlDataSource("lms-enrollments");
builder.Services.AddDbContext<EnrollmentDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
            b => b.MigrationsHistoryTable("__EFMigrationsHistory", "public")
                  .MigrationsAssembly("LMS.EnrollmentService.Infrastructure"))
     .UseSnakeCaseNamingConvention());

var host = builder.Build();
await host.StartAsync();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Applying EnrollmentService migrations...");
await db.Database.MigrateAsync();
logger.LogInformation("Migrations applied successfully.");

await host.StopAsync();

internal sealed class MigrationTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string[] Roles => [];
}
