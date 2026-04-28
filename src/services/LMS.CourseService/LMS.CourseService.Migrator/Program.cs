using Microsoft.EntityFrameworkCore;
using Npgsql;
using LMS.CourseService.Domain.Abstractions;
using LMS.CourseService.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSingleton<ITenantContext>(new MigrationTenantContext());
builder.AddNpgsqlDataSource("lms-courses");
builder.Services.AddDbContext<CourseDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
     .UseSnakeCaseNamingConvention());
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

internal sealed class MigrationTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string[] Roles => [];
}
