using LMS.CourseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CourseDbContext>("lms-courses");
builder.Services.AddHostedService<MigrationRunner>();

await builder.Build().RunAsync();

public sealed class MigrationRunner(
    IServiceProvider sp,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationRunner> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CourseDbContext>();
        log.LogInformation("Applying migrations for {Db}", db.Database.GetDbConnection().Database);
        await db.Database.MigrateAsync(ct);
        log.LogInformation("Migrations applied");
        lifetime.StopApplication();
    }
}
