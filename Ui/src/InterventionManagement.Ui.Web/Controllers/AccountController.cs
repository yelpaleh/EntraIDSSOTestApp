using System.Security.Claims;
using System.Net.Http.Json;
using InterventionManagement.Ui.Application.DTOs;
using InterventionManagement.Ui.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Ui.Web.Controllers;

[AllowAnonymous]
public sealed class AccountController(IConfiguration configuration, IHttpClientFactory httpClientFactory) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? error = null) =>
      View(new LoginViewModel
      {
          EnableSSO = configuration.GetValue("AuthenticationFeatures:EnableSSO", true),
          EnableLocalLogin = configuration.GetValue("AuthenticationFeatures:EnableLocalLogin", false),
          ReturnUrl = returnUrl,
          ErrorMessage = error
      });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string? returnUrl = null)
    {
        if (!configuration.GetValue("AuthenticationFeatures:EnableSSO", true))
        {
            return RedirectToAction(nameof(Login), new { error = "Single sign-on is disabled.", returnUrl });
        }

        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? Url.Action("Index", "Dashboard") }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LocalLogin(string username, string password, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("AuthenticationFeatures:EnableLocalLogin", false))
        {
            return RedirectToAction(nameof(Login), new { error = "Local login is disabled.", returnUrl });
        }

        // Replace this mock gate with ASP.NET Core Identity or an approved enterprise credential store before production enablement.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return View(nameof(Login), new LoginViewModel
            {
                EnableSSO = configuration.GetValue("AuthenticationFeatures:EnableSSO", true),
                EnableLocalLogin = true,
                ReturnUrl = returnUrl,
                ErrorMessage = "Invalid local credentials.",
                Username = username
            });
        }

        var apiToken = await GetLocalApiTokenAsync(username, password, cancellationToken);
        if (apiToken is null)
        {
            return View(nameof(Login), new LoginViewModel
            {
                EnableSSO = configuration.GetValue("AuthenticationFeatures:EnableSSO", true),
                EnableLocalLogin = true,
                ReturnUrl = returnUrl,
                ErrorMessage = "Unable to obtain a local API token.",
                Username = username
            });
        }

        var claims = new[]
        {
      new Claim(ClaimTypes.Name, username),
      new Claim("oid", "00000000-0000-0000-0000-000000000111"),
      new Claim("api_access_token", apiToken.AccessToken)
    };

        await HttpContext.SignInAsync(
          CookieAuthenticationDefaults.AuthenticationScheme,
          new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return LocalRedirect(returnUrl ?? Url.Action("Index", "Dashboard") ?? "/");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        var callbackUrl = Url.Action("Login", "Account", values: null, protocol: Request.Scheme);
        if (configuration.GetValue("AuthenticationFeatures:EnableSSO", false))
        {
            return SignOut(new AuthenticationProperties { RedirectUri = callbackUrl },
              CookieAuthenticationDefaults.AuthenticationScheme,
              OpenIdConnectDefaults.AuthenticationScheme);
        }

        return SignOut(new AuthenticationProperties { RedirectUri = callbackUrl },
          CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private async Task<LocalLoginResponse?> GetLocalApiTokenAsync(string username, string password, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("InterventionApi");
        using var response = await client.PostAsJsonAsync("api/auth/local-login", new LocalLoginRequest(username, password), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LocalLoginResponse>(cancellationToken);
    }
}