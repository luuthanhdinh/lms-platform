using LMS.EnrollmentService.Domain.Repositories;
using LMS.EnrollmentService.Infrastructure.Repositories;
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
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

        return services;
    }
}
