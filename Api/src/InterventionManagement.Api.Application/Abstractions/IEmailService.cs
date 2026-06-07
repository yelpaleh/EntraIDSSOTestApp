namespace InterventionManagement.Api.Application.Abstractions;

public interface IEmailService
{
    Task SendInterventionNotificationAsync(string mailbox, string recipient, string subject, string body, CancellationToken cancellationToken);
}
