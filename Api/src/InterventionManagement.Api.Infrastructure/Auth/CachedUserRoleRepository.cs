using InterventionManagement.Api.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace InterventionManagement.Api.Infrastructure.Auth;

public sealed class CachedUserRoleRepository(IMemoryCache cache) : IUserRoleRepository
{
    private static readonly IReadOnlyDictionary<string, string[]> MockRoleMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["00000000-0000-0000-0000-000000000111"] = ["Requester", "Approver"],
        ["00000000-0000-0000-0000-000000000222"] = ["Requester"],
        ["default"] = ["Requester"]
    };

    public Task<IReadOnlyCollection<string>> GetRolesForObjectIdAsync(string objectId, CancellationToken cancellationToken)
    {
        var roles = cache.GetOrCreate($"roles:{objectId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return MockRoleMap.TryGetValue(objectId, out var mappedRoles)
                ? mappedRoles
                : MockRoleMap["default"];
        });

        return Task.FromResult<IReadOnlyCollection<string>>(roles ?? []);
    }
}
