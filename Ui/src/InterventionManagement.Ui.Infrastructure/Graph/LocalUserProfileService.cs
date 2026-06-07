using System.Security.Claims;
using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Core.Entities;
using Microsoft.AspNetCore.Http;

namespace InterventionManagement.Ui.Infrastructure.Graph;

public sealed class LocalUserProfileService(IHttpContextAccessor httpContextAccessor, IInterventionApiClient apiClient) : IUserProfileService
{
    public async Task<AuthenticatedUserProfile> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var displayName = user?.Identity?.Name ?? "Local user";
        var roles = await GetDatabaseRolesAsync(cancellationToken);

        return new AuthenticatedUserProfile(displayName, string.Empty, null, roles);
    }

    private async Task<IReadOnlyCollection<string>> GetDatabaseRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await apiClient.GetCurrentAuthorizationAsync(cancellationToken)).Roles;
        }
        catch
        {
            return httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().OrderBy(x => x).ToArray() ?? [];
        }
    }
}
