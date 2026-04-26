using LMS.IdentityService.Domain.Repositories;
using LMS.IdentityService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ITenantConfigRepository, TenantConfigRepository>();
        services.AddScoped<IUserInviteRepository, UserInviteRepository>();
        return services;
    }
}
