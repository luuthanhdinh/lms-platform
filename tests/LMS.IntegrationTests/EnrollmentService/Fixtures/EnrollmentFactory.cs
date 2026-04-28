using LMS.EnrollmentService.Infrastructure.Data;
using Xunit;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace LMS.IntegrationTests.EnrollmentService.Fixtures;

/// <summary>
/// WebApplicationFactory for LMS.EnrollmentService.Api with:
///   - Testcontainers PostgreSQL replacing the real connection string
///   - MassTransit InMemoryTestHarness replacing RabbitMQ
///   - CourseArchivedConsumer registered so publish-to-consume flows work
/// </summary>
public sealed class EnrollmentFactory : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("lms_enrollment_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ITestHarness Harness { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Build the host (which will run ConfigureWebHost).
        // Accessing Services triggers host initialization.
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
            // Remove the real DbContext registration added by AddEnrollmentInfrastructure
            services.RemoveAll<DbContextOptions<EnrollmentDbContext>>();
            services.RemoveAll<EnrollmentDbContext>();

            services.AddDbContext<EnrollmentDbContext>((sp, options) =>
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
                x.AddConsumer<LMS.EnrollmentService.Infrastructure.Consumers.CourseArchivedConsumer>();
            });
        });

        // Suppress Aspire service-defaults OTEL / health that require external infra
        builder.UseEnvironment("Testing");
    }

    private async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
        await db.Database.MigrateAsync();
    }

    // ---------- helpers ----------

    /// <summary>
    /// Creates an HttpClient with the three forwarded-header claims that
    /// the EnrollmentService trusts (set by YARP gateway in production).
    /// </summary>
    public HttpClient CreateClientFor(Guid tenantId, Guid userId, string roles = "student")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Roles", roles);
        return client;
    }
}
