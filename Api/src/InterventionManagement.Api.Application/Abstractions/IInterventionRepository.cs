using InterventionManagement.Api.Core.Entities;
using InterventionManagement.Api.Core.Enums;

namespace InterventionManagement.Api.Application.Abstractions;

public interface IInterventionRepository
{
    Task<IReadOnlyList<Intervention>> ListAsync(CancellationToken cancellationToken);
    Task<Intervention?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Intervention> UpsertAsync(Intervention intervention, CancellationToken cancellationToken);
    Task<Intervention?> ChangeStatusAsync(Guid id, InterventionStatus status, CancellationToken cancellationToken);
}
