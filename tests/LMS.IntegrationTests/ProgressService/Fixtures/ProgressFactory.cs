using LMS.ProgressService.Infrastructure.Consumers;
using LMS.ProgressService.Infrastructure.Data;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LMS.IntegrationTests.ProgressService.Fixtures;

/// <summary>
/// WebApplicationFactory for LMS.ProgressService.Api with:
///   - Testcontainers PostgreSQL replacing the real connection string
///   - MassTransit InMemoryTestHarness replacing RabbitMQ
///   - UserEnrolledConsumer and EnrollmentCancelledConsumer registered
/// </summary>
public sealed class ProgressFactory : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("lms_progress_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ITestHarness Harness { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Services;
        await MigrateAsync();
        Harness = Services.GetRequiredService<ITestHarness>();
        await Harness.Start();
    }

    public new async Task DisposeAsync()
    {
        if (Harness is not null)
            await Harness.Stop();
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration added by AddProgressInfrastructure
            services.RemoveAll<DbContextOptions<ProgressDbContext>>();
            services.RemoveAll<ProgressDbContext>();

            services.AddDbContext<ProgressDbContext>((sp, options) =>
            {
                options.UseNpgsql(_postgres.GetConnectionString());
                options.UseSnakeCaseNamingConvention();
            });

            // Remove the real MassTransit / RabbitMQ bus
            services.RemoveAll<IBusControl>();
            services.RemoveAll<IBus>();
            services.RemoveAll<IPublishEndpoint>();
            services.RemoveAll<ISendEndpointProvider>();

            // Replace with in-memory test harness + consumers
            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<UserEnrolledConsumer>();
                x.AddConsumer<EnrollmentCancelledConsumer>();
            });
        });

        builder.UseEnvironment("Testing");
    }

    private async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates an HttpClient with the three forwarded-header claims that
    /// the ProgressService trusts (set by YARP gateway in production).
    /// </summary>
    public HttpClient CreateClientFor(Guid tenantId, Guid userId, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Roles", string.Join(",", roles.Length > 0 ? roles : new[] { "student" }));
        return client;
    }
}
