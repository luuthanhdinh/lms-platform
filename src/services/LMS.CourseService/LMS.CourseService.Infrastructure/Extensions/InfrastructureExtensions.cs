using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.CourseService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddCourseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ICourseSnapshotRepository, CourseSnapshotRepository>();

        return services;
    }
}
