using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Ui.Web.Controllers;

[Authorize]
public sealed class SettingsController(IInterventionApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetEmailSettingsAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(EmailNotificationSettingsDto model, CancellationToken cancellationToken)
    {
        await apiClient.SaveEmailSettingsAsync(model, cancellationToken);
        TempData["StatusMessage"] = "Email notification settings saved.";
        return RedirectToAction(nameof(Index));
    }
}
