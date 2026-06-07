using System.Security.Claims;
using InterventionManagement.Api.Application.DTOs;
using InterventionManagement.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterventionManagement.Api.Web.Controllers;

[ApiController]
[Route("api/interventions")]
[Authorize]
public sealed class InterventionsController(InterventionService interventions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InterventionDto>>> List(CancellationToken cancellationToken) =>
        Ok(await interventions.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InterventionDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var intervention = await interventions.GetAsync(id, cancellationToken);
        return intervention is null ? NotFound() : Ok(intervention);
    }

    [HttpPost]
    [Authorize(Roles = "Requester")]
    public async Task<ActionResult<InterventionDto>> Create(UpsertInterventionRequest request, CancellationToken cancellationToken)
    {
        var objectId = User.FindFirstValue("oid") ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Forbid();
        }

        var created = await interventions.UpsertAsync(null, objectId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Requester")]
    public async Task<ActionResult<InterventionDto>> Update(Guid id, UpsertInterventionRequest request, CancellationToken cancellationToken)
    {
        var objectId = User.FindFirstValue("oid") ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        return Ok(await interventions.UpsertAsync(id, objectId ?? "unknown", request, cancellationToken));
    }

    [HttpPost("{id:guid}/actions")]
    [Authorize(Roles = "Approver")]
    public async Task<ActionResult<InterventionDto>> ApplyAction(Guid id, InterventionActionRequest request, CancellationToken cancellationToken)
    {
        var intervention = await interventions.ApplyActionAsync(id, request.Action, cancellationToken);
        return intervention is null ? NotFound() : Ok(intervention);
    }
}
