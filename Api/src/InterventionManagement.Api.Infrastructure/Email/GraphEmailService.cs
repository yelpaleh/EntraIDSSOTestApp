using InterventionManagement.Api.Application.Abstractions;
using Microsoft.Graph;

namespace InterventionManagement.Api.Infrastructure.Email;

public sealed class GraphEmailService(GraphServiceClient graphClient) : IEmailService
{
    public async Task SendInterventionNotificationAsync(string mailbox, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Text, Content = body },
            From = new Recipient { EmailAddress = new EmailAddress { Address = mailbox } },
            ToRecipients =
            [
                new Recipient { EmailAddress = new EmailAddress { Address = recipient } }
            ]
        };

        await graphClient.Me.SendMail(message, true).Request().PostAsync(cancellationToken);
    }
}
