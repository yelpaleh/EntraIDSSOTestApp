using InterventionManagement.Ui.Core.Entities;

namespace InterventionManagement.Ui.Application.Abstractions;

public interface IUserProfileService
{
    Task<AuthenticatedUserProfile> GetCurrentUserAsync(CancellationToken cancellationToken);
}
