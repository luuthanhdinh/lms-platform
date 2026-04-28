using LMS.AssessmentService.Infrastructure.Consumers;
using LMS.AssessmentService.Infrastructure.Data;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace LMS.IntegrationTests.AssessmentService.Fixtures;

/// <summary>
/// WebApplicationFactory for LMS.AssessmentService.Api with:
///   - Testcontainers PostgreSQL (postgres:16-alpine) replacing the real connection string
///   - Testcontainers Redis (redis:7-alpine) replacing the real Redis connection
///   - MassTransit InMemoryTestHarness replacing RabbitMQ
///   - CourseArchivedConsumer registered so publish-to-consume flows work
/// </summary>
public sealed class AssessmentFactory : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("lms_assessment_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public ITestHarness Harness { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
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
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real DbContext
            services.RemoveAll<DbContextOptions<AssessmentDbContext>>();
            services.RemoveAll<AssessmentDbContext>();

            services.AddDbContext<AssessmentDbContext>((sp, options) =>
            {
                options.UseNpgsql(_postgres.GetConnectionString());
                options.UseSnakeCaseNamingConvention();
            });

            // Remove real Redis
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Remove real MassTransit / RabbitMQ bus
            services.RemoveAll<IBusControl>();
            services.RemoveAll<IBus>();
            services.RemoveAll<IPublishEndpoint>();
            services.RemoveAll<ISendEndpointProvider>();

            // Replace with in-memory test harness + consumers
            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<CourseArchivedConsumer>();
            });
        });

        builder.UseEnvironment("Testing");
    }

    private async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates an HttpClient with the three forwarded-header claims that
    /// the AssessmentService trusts (set by YARP gateway in production).
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
