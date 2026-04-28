using LMS.AssessmentService.Domain.Abstractions;
using LMS.AssessmentService.Domain.Repositories;
using LMS.AssessmentService.Domain.Sessions;
using LMS.AssessmentService.Infrastructure.Auth;
using LMS.AssessmentService.Infrastructure.Data;
using LMS.AssessmentService.Infrastructure.Repositories;
using LMS.AssessmentService.Infrastructure.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace LMS.AssessmentService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddAssessmentInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HeaderTenantContext>();

        services.AddDbContext<AssessmentDbContext>((sp, opts) =>
            opts.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
                    b => b.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAssessmentRepository, AssessmentRepository>();
        services.AddScoped<IAttemptRepository, AttemptRepository>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config.GetConnectionString("redis") ?? "localhost:6379"));

        services.AddScoped<IAssessmentSessionService, RedisAssessmentSessionService>();

        return services;
    }
}
