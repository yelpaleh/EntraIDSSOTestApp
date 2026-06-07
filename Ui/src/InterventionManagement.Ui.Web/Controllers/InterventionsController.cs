using InterventionManagement.Ui.Application.Abstractions;
using InterventionManagement.Ui.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Ui.Web.Controllers;

[Authorize]
public sealed class InterventionsController(IInterventionApiClient apiClient) : Controller
{
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var intervention = await apiClient.GetAsync(id, cancellationToken);
        return intervention is null ? NotFound() : View(intervention);
    }

    public IActionResult Create() => View("Edit", new InterventionFormViewModel());

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var intervention = await apiClient.GetAsync(id, cancellationToken);
        return intervention is null ? NotFound() : View(InterventionFormViewModel.FromDto(intervention));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(InterventionFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        if (model.Id.HasValue)
        {
            await apiClient.UpdateAsync(model.Id.Value, model.ToRequest(), cancellationToken);
        }
        else
        {
            await apiClient.CreateAsync(model.ToRequest(), cancellationToken);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Action(Guid id, string action, CancellationToken cancellationToken)
    {
        await apiClient.ApplyActionAsync(id, action, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}
