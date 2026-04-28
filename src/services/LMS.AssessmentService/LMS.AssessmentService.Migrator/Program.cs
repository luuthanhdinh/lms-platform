using LMS.AssessmentService.Infrastructure.Data;
using LMS.AssessmentService.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddAssessmentInfrastructure(builder.Configuration);

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
            var db = scope.ServiceProvider.GetRequiredService<AssessmentDbContext>();
            logger.LogInformation("Applying AssessmentService migrations...");
            await db.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("AssessmentService migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AssessmentService migration failed.");
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
