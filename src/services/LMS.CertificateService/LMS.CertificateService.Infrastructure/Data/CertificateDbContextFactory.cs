using LMS.CertificateService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LMS.CertificateService.Infrastructure.Data;

public class CertificateDbContextFactory : IDesignTimeDbContextFactory<CertificateDbContext>
{
    public CertificateDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CertificateDbContext>()
            .UseNpgsql("Host=localhost;Database=lms-certificate;Username=postgres;Password=lms_dev_pg",
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "public")
                      .MigrationsAssembly("LMS.CertificateService.Infrastructure"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CertificateDbContext(options, new DesignTimeTenantContext());
    }
}

internal sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
}
