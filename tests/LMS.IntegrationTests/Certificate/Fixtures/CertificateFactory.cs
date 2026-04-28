using LMS.CertificateService.Domain.Interfaces;
using CertEntity = LMS.CertificateService.Domain.Entities.Certificate;
using LMS.CertificateService.Infrastructure.Data;
using LMS.CertificateService.Infrastructure.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LMS.IntegrationTests.Certificate.Fixtures;

public sealed class CertificateFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("lms-certificate-test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public readonly StubCertificateStorageService StorageStub = new();
    public readonly StubCertificatePdfGenerator PdfStub = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DbContext with test container
            services.RemoveAll<DbContextOptions<CertificateDbContext>>();
            services.RemoveAll<CertificateDbContext>();

            services.AddDbContext<CertificateDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(_postgres.GetConnectionString());
            });

            // Replace storage + pdf generator with stubs
            services.RemoveAll<ICertificateStorageService>();
            services.RemoveAll<ICertificatePdfGenerator>();
            services.AddSingleton<ICertificateStorageService>(StorageStub);
            services.AddSingleton<ICertificatePdfGenerator>(PdfStub);

            // MassTransit in-memory test harness
            services.RemoveAll<IBusControl>();
            services.AddMassTransitTestHarness(x =>
            {
                x.AddEntityFrameworkOutbox<CertificateDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });

                x.AddConsumer<LMS.CertificateService.Infrastructure.Consumers.CourseCompletedConsumer>();
            });

            // Migrate database
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CertificateDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public HttpClient CreateClientWithHeaders(Guid tenantId, Guid userId, string roles = "student")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Roles", roles);
        return client;
    }
}

public sealed class StubCertificateStorageService : ICertificateStorageService
{
    private readonly Dictionary<string, byte[]> _store = new();

    public Task UploadAsync(string key, Stream pdf, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        pdf.CopyTo(ms);
        _store[key] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<string> GetDownloadUrlAsync(string key, CancellationToken ct = default)
        => Task.FromResult($"https://stub-storage/{key}");

    public Task<Stream> GetStreamAsync(string key, CancellationToken ct = default)
    {
        var bytes = _store.TryGetValue(key, out var data) ? data : Array.Empty<byte>();
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public bool HasKey(string key) => _store.ContainsKey(key);
}

public sealed class StubCertificatePdfGenerator : ICertificatePdfGenerator
{
    // Minimal valid-looking byte array
    private static readonly byte[] MinimalPdf = "%PDF-1.4 stub certificate"u8.ToArray();

    public Task<byte[]> GenerateAsync(CertEntity cert, CancellationToken ct = default)
        => Task.FromResult(MinimalPdf);
}
