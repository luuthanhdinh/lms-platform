using LMS.IdentityService.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LMS.IdentityService.Infrastructure.Data;

public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=lms_identity;Username=postgres;Password=postgres")
            .Options;

        return new IdentityDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public string[] Roles => [];
    }
}
