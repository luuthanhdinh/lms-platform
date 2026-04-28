using LMS.EnrollmentService.Infrastructure.Data;
using LMS.EnrollmentService.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddEnrollmentInfrastructure(builder.Configuration);

builder.Services.AddHostedService<MigratorService>();

var host = builder.Build();
await host.RunAsync();

internal sealed class MigratorService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<MigratorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
            logger.LogInformation("Applying EnrollmentService migrations...");
            await db.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("Migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed.");
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
