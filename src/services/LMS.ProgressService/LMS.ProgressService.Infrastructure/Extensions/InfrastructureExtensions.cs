using LMS.ProgressService.Domain.Repositories;
using LMS.ProgressService.Infrastructure.Data;
using LMS.ProgressService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.ProgressService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers EF Core DbContext and repositories.
    /// MassTransit is NOT configured here — Api and Migrator configure the bus independently.
    /// </summary>
    public static IServiceCollection AddProgressInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<ProgressDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("lms-progress")));

        services.AddScoped<ILessonProgressRepository, LessonProgressRepository>();
        services.AddScoped<ICourseProgressRepository, CourseProgressRepository>();

        return services;
    }
}
