using System.Security.Claims;
using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Graph;

namespace InterventionManagement.Ui.Infrastructure.Graph;

public sealed class GraphUserProfileService(
    GraphServiceClient graphClient,
    IHttpContextAccessor httpContextAccessor,
    IInterventionApiClient apiClient) : IUserProfileService
{
    public async Task<AuthenticatedUserProfile> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var me = await graphClient.Me.Request().GetAsync(cancellationToken);
        var roles = await GetDatabaseRolesAsync(cancellationToken);

        string? photoDataUrl = null;
        try
        {
            await using var photo = await graphClient.Me.Photo.Content.Request().GetAsync(cancellationToken);
            if (photo is not null)
            {
                using var memory = new MemoryStream();
                await photo.CopyToAsync(memory, cancellationToken);
                photoDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(memory.ToArray())}";
            }
        }
        catch
        {
            photoDataUrl = null;
        }

        return new AuthenticatedUserProfile(
            me?.DisplayName ?? "Authenticated user",
            me?.Mail ?? me?.UserPrincipalName ?? string.Empty,
            photoDataUrl,
            roles);
    }

    private async Task<IReadOnlyCollection<string>> GetDatabaseRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var authorization = await apiClient.GetCurrentAuthorizationAsync(cancellationToken);
            return authorization.Roles;
        }
        catch
        {
            return httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().OrderBy(x => x).ToArray() ?? [];
        }
    }
}
