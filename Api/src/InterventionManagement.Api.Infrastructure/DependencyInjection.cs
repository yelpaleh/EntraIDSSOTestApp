using InterventionManagement.Api.Application.Abstractions;
using InterventionManagement.Api.Application.Services;
using InterventionManagement.Api.Infrastructure.Auth;
using InterventionManagement.Api.Infrastructure.Email;
using InterventionManagement.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace InterventionManagement.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool enableSso)
    {
        services.AddMemoryCache();
        services.AddScoped<InterventionService>();
        services.AddSingleton<IInterventionRepository, MockInterventionRepository>();
        services.AddScoped<IUserRoleRepository, CachedUserRoleRepository>();
        if (enableSso)
        {
            services.AddScoped<IEmailService, GraphEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, LocalEmailService>();
        }

        return services;
    }
}
