using InterventionManagement.Api.Application.DTOs;
using InterventionManagement.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Api.Web.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Requester,Approver")]
public sealed class ReportsController(InterventionService interventions) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<IReadOnlyList<InterventionDto>> Dashboard(CancellationToken cancellationToken) =>
        interventions.ListAsync(cancellationToken);

    [HttpGet("calendar")]
    public async Task<IReadOnlyList<CalendarEventDto>> Calendar(CancellationToken cancellationToken)
    {
        var rows = await interventions.ListAsync(cancellationToken);
        return rows.Select(x => new CalendarEventDto(x.Id, x.Title, x.ScheduledDate, x.Status.ToString())).ToArray();
    }
}
