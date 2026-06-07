//using System.Net.Http.Headers;
//using System.Net.Http.Json;
//using InterventionManagement.Ui.Application.Abstractions;
//using InterventionManagement.Ui.Application.DTOs;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Identity.Web;

//namespace InterventionManagement.Ui.Infrastructure.Api;

//public sealed class SecureInterventionApiClient(
//    IHttpClientFactory httpClientFactory,
//    IServiceProvider serviceProvider,
//    IHttpContextAccessor httpContextAccessor,
//    IConfiguration configuration) : IInterventionApiClient
//{
//    private readonly string[] _scopes = configuration.GetSection("DownstreamApi:Scopes").Get<string[]>() ?? [];

//    public Task<IReadOnlyList<InterventionDto>> GetDashboardAsync(CancellationToken cancellationToken) =>
//        GetAsync<IReadOnlyList<InterventionDto>>("api/reports/dashboard", cancellationToken);

//    public Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(CancellationToken cancellationToken) =>
//        GetAsync<IReadOnlyList<CalendarEventDto>>("api/reports/calendar", cancellationToken);

//    public Task<InterventionDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
//        GetNullableAsync<InterventionDto>($"api/interventions/{id}", cancellationToken);

//    public async Task CreateAsync(UpsertInterventionRequest request, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        using var response = await client.PostAsJsonAsync("api/interventions", request, cancellationToken);
//        response.EnsureSuccessStatusCode();
//    }

//    public async Task UpdateAsync(Guid id, UpsertInterventionRequest request, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        using var response = await client.PutAsJsonAsync($"api/interventions/{id}", request, cancellationToken);
//        response.EnsureSuccessStatusCode();
//    }

//    public async Task ApplyActionAsync(Guid id, string action, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        using var response = await client.PostAsJsonAsync($"api/interventions/{id}/actions", new InterventionActionRequest(action), cancellationToken);
//        response.EnsureSuccessStatusCode();
//    }

//    public Task<EmailNotificationSettingsDto> GetEmailSettingsAsync(CancellationToken cancellationToken) =>
//        GetAsync<EmailNotificationSettingsDto>("api/settings/email-notifications", cancellationToken);

//    public async Task SaveEmailSettingsAsync(EmailNotificationSettingsDto request, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        using var response = await client.PostAsJsonAsync("api/settings/email-notifications", request, cancellationToken);
//        response.EnsureSuccessStatusCode();
//    }

//    public Task<UserAuthorizationDto> GetCurrentAuthorizationAsync(CancellationToken cancellationToken) =>
//        GetAsync<UserAuthorizationDto>("api/me/authorization", cancellationToken);

//    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        return await client.GetFromJsonAsync<T>(path, cancellationToken) ?? throw new InvalidOperationException($"API returned no payload for {path}.");
//    }

//    private async Task<T?> GetNullableAsync<T>(string path, CancellationToken cancellationToken)
//    {
//        var client = await CreateClientAsync(cancellationToken);
//        using var response = await client.GetAsync(path, cancellationToken);
//        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
//        {
//            return default;
//        }

//        response.EnsureSuccessStatusCode();
//        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
//    }

//    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
//    {
//        var client = httpClientFactory.CreateClient("InterventionApi");
//        var token = httpContextAccessor.HttpContext?.User.FindFirst("api_access_token")?.Value;
//        if (string.IsNullOrWhiteSpace(token))
//        {
//            var tokenAcquisition = serviceProvider.GetRequiredService<ITokenAcquisition>();
//            token = await tokenAcquisition.GetAccessTokenForUserAsync(_scopes);
//        }

//        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
//        return client;
//    }
//}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace InterventionManagement.Ui.Infrastructure.Api;

public sealed class SecureInterventionApiClient(
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IInterventionApiClient
{
    // BULLETPROOF SCOPE PARSING
    private readonly string[] _scopes = configuration.GetSection("DownstreamApi:Scopes").Get<string[]>()
        ?? new[] { configuration["DownstreamApi:Scopes"] ?? string.Empty }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

    public Task<IReadOnlyList<InterventionDto>> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<InterventionDto>>("api/reports/dashboard", cancellationToken);

    public Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<CalendarEventDto>>("api/reports/calendar", cancellationToken);

    public Task<InterventionDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        GetNullableAsync<InterventionDto>($"api/interventions/{id}", cancellationToken);

    public async Task CreateAsync(UpsertInterventionRequest request, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.PostAsJsonAsync("api/interventions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Guid id, UpsertInterventionRequest request, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.PutAsJsonAsync($"api/interventions/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyActionAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.PostAsJsonAsync($"api/interventions/{id}/actions", new InterventionActionRequest(action), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<EmailNotificationSettingsDto> GetEmailSettingsAsync(CancellationToken cancellationToken) =>
        GetAsync<EmailNotificationSettingsDto>("api/settings/email-notifications", cancellationToken);

    public async Task SaveEmailSettingsAsync(EmailNotificationSettingsDto request, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.PostAsJsonAsync("api/settings/email-notifications", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<UserAuthorizationDto> GetCurrentAuthorizationAsync(CancellationToken cancellationToken) =>
        GetAsync<UserAuthorizationDto>("api/me/authorization", cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        return await client.GetFromJsonAsync<T>(path, cancellationToken) ?? throw new InvalidOperationException($"API returned no payload for {path}.");
    }

    private async Task<T?> GetNullableAsync<T>(string path, CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("InterventionApi");
        var token = httpContextAccessor.HttpContext?.User.FindFirst("api_access_token")?.Value;

        if (string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var tokenAcquisition = serviceProvider.GetRequiredService<ITokenAcquisition>();
                token = await tokenAcquisition.GetAccessTokenForUserAsync(_scopes);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"MSAL failed to acquire a token for the scopes: {string.Join(",", _scopes)}", ex);
            }
        }

        // Fail-fast if we somehow bypassed the above and still have no token
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedAccessException("Authentication token is missing. Cannot call the downstream API.");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}