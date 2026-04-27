using LMS.ProgressService.Infrastructure.Data;
using LMS.ProgressService.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddProgressInfrastructure(builder.Configuration);

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
            var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
            logger.LogInformation("Applying ProgressService migrations...");
            await db.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("ProgressService migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProgressService migration failed.");
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
