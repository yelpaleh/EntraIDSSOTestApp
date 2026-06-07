using InterventionManagement.Api.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Api.Web.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IConfiguration configuration, LocalJwtTokenService tokenService) : ControllerBase
{
    [HttpPost("local-login")]
    public ActionResult<LocalLoginResponse> LocalLogin(LocalLoginRequest request)
    {
        if (!configuration.GetValue("AuthenticationFeatures:EnableLocalLogin", false))
        {
            return NotFound();
        }

        // Replace this mock gate with ASP.NET Core Identity or an approved enterprise credential store before production enablement.
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized();
        }

        var objectId = request.Username.Equals("approver", StringComparison.OrdinalIgnoreCase)
            ? "00000000-0000-0000-0000-000000000111"
            : "00000000-0000-0000-0000-000000000222";

        return Ok(new LocalLoginResponse(tokenService.CreateToken(request.Username, objectId), "Bearer", 3600));
    }
}

public sealed record LocalLoginRequest(string Username, string Password);

public sealed record LocalLoginResponse(string AccessToken, string TokenType, int ExpiresIn);
