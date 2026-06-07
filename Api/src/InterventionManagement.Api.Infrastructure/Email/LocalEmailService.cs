using InterventionManagement.Api.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace InterventionManagement.Api.Infrastructure.Email;

public sealed class LocalEmailService(ILogger<LocalEmailService> logger) : IEmailService
{
    public Task SendInterventionNotificationAsync(string mailbox, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Local email notification suppressed. Mailbox: {Mailbox}, Recipient: {Recipient}, Subject: {Subject}",
            mailbox,
            recipient,
            subject);

        return Task.CompletedTask;
    }
}
