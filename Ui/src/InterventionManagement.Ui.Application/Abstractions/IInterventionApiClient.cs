using InterventionManagement.Ui.Application.DTOs;

namespace InterventionManagement.Ui.Application.Abstractions;

public interface IInterventionApiClient
{
    Task<IReadOnlyList<InterventionDto>> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CalendarEventDto>> GetCalendarAsync(CancellationToken cancellationToken);
    Task<InterventionDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task CreateAsync(UpsertInterventionRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(Guid id, UpsertInterventionRequest request, CancellationToken cancellationToken);
    Task ApplyActionAsync(Guid id, string action, CancellationToken cancellationToken);
    Task<EmailNotificationSettingsDto> GetEmailSettingsAsync(CancellationToken cancellationToken);
    Task SaveEmailSettingsAsync(EmailNotificationSettingsDto request, CancellationToken cancellationToken);
    Task<UserAuthorizationDto> GetCurrentAuthorizationAsync(CancellationToken cancellationToken);
}
