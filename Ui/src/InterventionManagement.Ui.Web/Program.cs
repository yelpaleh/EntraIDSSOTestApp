using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using InterventionManagement.Ui.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

var vaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(vaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential(), new AzureKeyVaultConfigurationOptions());
}

var apiScopes = builder.Configuration.GetSection("DownstreamApi:Scopes").Get<string[]>() ?? [];
var enableSso = builder.Configuration.GetValue("AuthenticationFeatures:EnableSSO", false);

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
  .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
  {
      options.LoginPath = "/Account/Login";
      options.AccessDeniedPath = "/Account/Login";
  });

if (enableSso)
{
    authBuilder.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
      .EnableTokenAcquisitionToCallDownstreamApi(apiScopes)
      .AddMicrosoftGraph(options =>
      {
          options.BaseUrl = builder.Configuration["Graph:BaseUrl"] ?? "https://graph.microsoft.com/v1.0";
          options.Scopes = builder.Configuration["Graph:Scopes"] ?? "User.Read Mail.Send";
      })
      .AddInMemoryTokenCaches();
}

builder.Services.AddAuthorization();
builder.Services.AddInfrastructure(builder.Configuration, enableSso);
builder.Services.AddControllersWithViews()
  .AddMicrosoftIdentityUI();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
