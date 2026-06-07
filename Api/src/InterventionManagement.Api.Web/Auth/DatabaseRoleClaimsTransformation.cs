using System.Security.Claims;
using InterventionManagement.Api.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace InterventionManagement.Api.Web.Auth;

public sealed class DatabaseRoleClaimsTransformation(IUserRoleRepository roles) : IClaimsTransformation
{
    private const string TransformationMarker = "imp.roles.transformed";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(TransformationMarker, "true"))
        {
            return principal;
        }

        var objectId = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

        if (string.IsNullOrWhiteSpace(objectId) || principal.Identity is not ClaimsIdentity identity)
        {
            return principal;
        }

        foreach (var tokenRole in identity.Claims
            .Where(claim => claim.Type == ClaimTypes.Role || claim.Type == identity.RoleClaimType || claim.Type == "roles")
            .ToArray())
        {
            identity.RemoveClaim(tokenRole);
        }

        foreach (var role in await roles.GetRolesForObjectIdAsync(objectId, CancellationToken.None))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        identity.AddClaim(new Claim(TransformationMarker, "true"));
        return principal;
    }
}
