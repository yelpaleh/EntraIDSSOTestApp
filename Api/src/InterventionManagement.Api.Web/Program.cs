using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using InterventionManagement.Api.Infrastructure;
using InterventionManagement.Api.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

var vaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrWhiteSpace(vaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential(), new AzureKeyVaultConfigurationOptions());
}

var enableSso = builder.Configuration.GetValue("AuthenticationFeatures:EnableSSO", false);
var localJwtSigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
builder.Services.AddSingleton(localJwtSigningKey);
builder.Services.AddScoped<LocalJwtTokenService>();

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthenticationSchemes.DynamicBearer;
    options.DefaultChallengeScheme = AuthenticationSchemes.DynamicBearer;
});

authBuilder.AddPolicyScheme(AuthenticationSchemes.DynamicBearer, AuthenticationSchemes.DynamicBearer, options =>
{
    options.ForwardDefaultSelector = context =>
    {
        if (!enableSso)
        {
            return AuthenticationSchemes.LocalBearer;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationSchemes.EntraBearer;
        }

        var token = authorization["Bearer ".Length..].Trim();
        var issuer = new JwtSecurityTokenHandler().CanReadToken(token)
            ? new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer
            : string.Empty;

        return issuer.Equals(builder.Configuration["LocalJwt:Issuer"] ?? "InterventionManagement.Local", StringComparison.OrdinalIgnoreCase)
            ? AuthenticationSchemes.LocalBearer
            : AuthenticationSchemes.EntraBearer;
    };
});

authBuilder.AddJwtBearer(AuthenticationSchemes.LocalBearer, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["LocalJwt:Issuer"] ?? "InterventionManagement.Local",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["LocalJwt:Audience"] ?? "InterventionManagement.Api",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = localJwtSigningKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        RoleClaimType = ClaimTypes.Role
    };
});

if (enableSso)
{
    authBuilder.AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"),
        jwtBearerScheme: AuthenticationSchemes.EntraBearer);
}

builder.Services.AddAuthorization();
builder.Services.AddScoped<IClaimsTransformation, DatabaseRoleClaimsTransformation>();
builder.Services.AddInfrastructure(enableSso);
if (enableSso)
{
    builder.Services.AddMicrosoftGraph(options =>
    {
        options.BaseUrl = builder.Configuration["Graph:BaseUrl"] ?? "https://graph.microsoft.com/v1.0";
        options.Scopes = builder.Configuration["Graph:Scopes"] ?? "User.Read Mail.Send";
    });
}
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a local token from /api/auth/local-login, or an Entra ID access token when SSO is enabled."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });

    if (enableSso)
    {
        var tenantId = builder.Configuration["AzureAd:TenantId"];
        options.AddSecurityDefinition("EntraID", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
                    TokenUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"),
                    Scopes = builder.Configuration.GetSection("Swagger:OAuthScopes").Get<Dictionary<string, string>>() ?? []
                }
            }
        });
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId(builder.Configuration["Swagger:OAuthClientId"]);
        options.OAuthUsePkce();
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
