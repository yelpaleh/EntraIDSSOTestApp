namespace InterventionManagement.Api.Application.Abstractions;

public interface IUserRoleRepository
{
    Task<IReadOnlyCollection<string>> GetRolesForObjectIdAsync(string objectId, CancellationToken cancellationToken);
}
