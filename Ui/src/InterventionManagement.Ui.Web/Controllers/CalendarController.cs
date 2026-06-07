using InterventionManagement.Ui.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Ui.Web.Controllers;

[Authorize]
public sealed class CalendarController(IInterventionApiClient apiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await apiClient.GetCalendarAsync(cancellationToken));
}
