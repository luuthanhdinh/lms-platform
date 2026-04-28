using LMS.CertificateService.Domain.Interfaces;
using LMS.CertificateService.Domain.Repositories;
using LMS.CertificateService.Infrastructure.Auth;
using LMS.CertificateService.Infrastructure.Consumers;
using LMS.CertificateService.Infrastructure.Data;
using LMS.CertificateService.Infrastructure.Pdf;
using LMS.CertificateService.Infrastructure.Repositories;
using LMS.CertificateService.Infrastructure.Storage;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LMS.CertificateService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IHostApplicationBuilder AddCertificateInfrastructure(this IHostApplicationBuilder builder)
    {
        // DbContext
        builder.Services.AddDbContext<CertificateDbContext>((sp, options) =>
        {
            var connectionString = builder.Configuration["ConnectionStrings:certificatedb"]
                ?? builder.Configuration["ConnectionStrings:lms-certificate"];
            options.UseNpgsql(connectionString);
        });

        // Tenant context (scoped — reads X-Tenant-Id from HTTP headers)
        builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();

        // Unit of Work
        builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Repositories
        builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();

        // PDF generator (singleton — stateless)
        builder.Services.AddSingleton<ICertificatePdfGenerator, QuestPdfCertificateGenerator>();

        // Blob storage (singleton — BlobServiceClient is thread-safe)
        builder.AddAzureBlobServiceClient("certificate-pdfs");
        builder.Services.AddSingleton<ICertificateStorageService, AzureBlobCertificateStorageService>();

        // MassTransit
        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<CertificateDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<CourseCompletedConsumer>(cfg =>
            {
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(30)));
            });

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var connectionString = builder.Configuration["ConnectionStrings:rabbitmq"];
                if (!string.IsNullOrEmpty(connectionString))
                    cfg.Host(connectionString);

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return builder;
    }
}
