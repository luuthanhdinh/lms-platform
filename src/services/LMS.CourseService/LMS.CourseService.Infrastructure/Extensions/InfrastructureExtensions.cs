using LMS.CourseService.Domain.Repositories;
using LMS.CourseService.Infrastructure.Consumers;
using LMS.CourseService.Infrastructure.Data;
using LMS.CourseService.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.CourseService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddCourseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CourseDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("lms-courses"));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ICourseSnapshotRepository, CourseSnapshotRepository>();

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<CourseDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<ContentProcessingCompletedConsumer>();
            x.AddConsumer<UserEnrolledConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("rabbitmq"));
                cfg.ConfigureEndpoints(ctx);
                cfg.UseMessageRetry(r => r.Intervals(500, 1000, 2000));
            });
        });

        return services;
    }
}
