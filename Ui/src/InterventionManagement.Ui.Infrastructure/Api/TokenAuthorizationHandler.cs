using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using Microsoft.Extensions.DependencyInjection;

namespace InterventionManagement.Ui.Infrastructure.Api
{
    public class TokenAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var context = httpContextAccessor.HttpContext;

            if (context?.User.Identity?.IsAuthenticated == true)
            {
                string? token = null;

                // Determine how the user logged in
                var isLocalLogin = context.User.FindFirst("idp")?.Value == "local";

                if (isLocalLogin)
                {
                    // 1. Fetch token stored securely in the local authentication cookie properties
                    token = await context.GetTokenAsync("access_token");
                }
                else
                {
                    // 2. Fetch token from MSAL Distributed Cache for Entra ID SSO
                    // We resolve this safely via Service Locator because ITokenAcquisition might not be registered if SSO is disabled
                    var tokenAcquisition = context.RequestServices.GetService<ITokenAcquisition>();
                    if (tokenAcquisition != null)
                    {
                        var scopes = configuration.GetSection("DownstreamApi:Scopes").Get<string[]>() ?? [];
                        token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes);
                    }
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
