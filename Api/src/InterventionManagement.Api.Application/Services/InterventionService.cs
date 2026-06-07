using InterventionManagement.Api.Application.Abstractions;
using InterventionManagement.Api.Application.DTOs;
using InterventionManagement.Api.Core.Entities;
using InterventionManagement.Api.Core.Enums;

namespace InterventionManagement.Api.Application.Services;

public sealed class InterventionService(IInterventionRepository repository)
{
    public async Task<IReadOnlyList<InterventionDto>> ListAsync(CancellationToken cancellationToken)
    {
        var interventions = await repository.ListAsync(cancellationToken);
        return interventions.Select(ToDto).ToArray();
    }

    public async Task<InterventionDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var intervention = await repository.GetAsync(id, cancellationToken);
        return intervention is null ? null : ToDto(intervention);
    }

    public async Task<InterventionDto> UpsertAsync(Guid? id, string requesterObjectId, UpsertInterventionRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Location);

        var intervention = new Intervention
        {
            Id = id ?? Guid.NewGuid(),
            Title = request.Title.Trim(),
            Location = request.Location.Trim(),
            RequesterObjectId = requesterObjectId,
            ScheduledDate = request.ScheduledDate,
            Priority = request.Priority,
            Status = id.HasValue ? InterventionStatus.Submitted : InterventionStatus.Draft,
            Description = request.Description?.Trim(),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        return ToDto(await repository.UpsertAsync(intervention, cancellationToken));
    }

    public async Task<InterventionDto?> ApplyActionAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var status = action.Trim().ToUpperInvariant() switch
        {
            "APPROVE" or "APPROVED" => InterventionStatus.Approved,
            "CANCEL" or "CANCELLED" => InterventionStatus.Cancelled,
            "COMPLETE" or "COMPLETED" => InterventionStatus.Completed,
            _ => throw new InvalidOperationException("Unsupported intervention action.")
        };

        var intervention = await repository.ChangeStatusAsync(id, status, cancellationToken);
        return intervention is null ? null : ToDto(intervention);
    }

    private static InterventionDto ToDto(Intervention intervention) =>
        new(
            intervention.Id,
            intervention.Title,
            intervention.Location,
            intervention.RequesterObjectId,
            intervention.ScheduledDate,
            intervention.Priority,
            intervention.Status,
            intervention.Description,
            intervention.UpdatedAtUtc);
}
