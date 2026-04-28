using LMS.CourseService.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LMS.CourseService.Infrastructure.Data;

/// <summary>
/// Design-time factory needed for `dotnet ef migrations add`.
/// Uses a stub tenant context with TenantId = Guid.Empty.
/// </summary>
public sealed class CourseDbContextFactory : IDesignTimeDbContextFactory<CourseDbContext>
{
    public CourseDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CourseDbContext>()
            .UseNpgsql("Host=localhost;Database=lms-courses;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CourseDbContext(options, new DesignTimeTenantContext());
    }
}

internal sealed class DesignTimeTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string[] Roles => Array.Empty<string>();
}
