using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace InterventionManagement.Api.Web.Auth;

public sealed class LocalJwtTokenService(SymmetricSecurityKey signingKey, IConfiguration configuration)
{
    public string CreateToken(string username, string objectId)
    {
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(ClaimTypes.Name, username),
            new Claim("oid", objectId)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["LocalJwt:Issuer"] ?? "InterventionManagement.Local",
            audience: configuration["LocalJwt:Audience"] ?? "InterventionManagement.Api",
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(configuration.GetValue("LocalJwt:TokenLifetimeMinutes", 60)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
