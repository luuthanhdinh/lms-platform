using LMS.EnrollmentService.Domain.Repositories;
using LMS.EnrollmentService.Infrastructure.Data;
using LMS.EnrollmentService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.EnrollmentService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers EF Core DbContext and repositories.
    /// MassTransit is NOT configured here — Api and Migrator configure the bus independently.
    /// </summary>
    public static IServiceCollection AddEnrollmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<EnrollmentDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("lms-enrollments"));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

        return services;
    }
}
