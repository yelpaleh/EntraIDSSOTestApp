using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Infrastructure.Api;
using InterventionManagement.Ui.Infrastructure.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterventionManagement.Ui.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool enableSso)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient("InterventionApi", client =>
        {
            client.BaseAddress = new Uri(configuration["DownstreamApi:BaseUrl"] ?? "https://localhost:7036/");
        });
        services.AddScoped<IInterventionApiClient, SecureInterventionApiClient>();
        if (enableSso)
        {
            services.AddScoped<IUserProfileService, GraphUserProfileService>();
        }
        else
        {
            services.AddScoped<IUserProfileService, LocalUserProfileService>();
        }
        return services;
    }
}
