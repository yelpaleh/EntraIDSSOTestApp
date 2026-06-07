using InterventionManagement.Api.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Api.Web.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "Approver")]
public sealed class SettingsController : ControllerBase
{
    [HttpGet("email-notifications")]
    public ActionResult<EmailNotificationSettingsDto> GetEmailNotifications() =>
        Ok(new EmailNotificationSettingsDto(true, "mailbox@contoso.com", "interventions@contoso.com"));

    [HttpPost("email-notifications")]
    public ActionResult<EmailNotificationSettingsDto> SaveEmailNotifications(EmailNotificationSettingsDto request) =>
        Ok(request);
}
