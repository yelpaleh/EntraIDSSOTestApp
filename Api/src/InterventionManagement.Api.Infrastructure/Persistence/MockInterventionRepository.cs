using System.Collections.Concurrent;
using InterventionManagement.Api.Application.Abstractions;
using InterventionManagement.Api.Core.Entities;
using InterventionManagement.Api.Core.Enums;

namespace InterventionManagement.Api.Infrastructure.Persistence;

public sealed class MockInterventionRepository : IInterventionRepository
{
    private static readonly ConcurrentDictionary<Guid, Intervention> Store = Seed();

    public Task<IReadOnlyList<Intervention>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Intervention>>(Store.Values.OrderBy(x => x.ScheduledDate).ToArray());

    public Task<Intervention?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Store.TryGetValue(id, out var intervention);
        return Task.FromResult(intervention);
    }

    public Task<Intervention> UpsertAsync(Intervention intervention, CancellationToken cancellationToken)
    {
        Store.AddOrUpdate(intervention.Id, intervention, (_, existing) =>
        {
            existing.Title = intervention.Title;
            existing.Location = intervention.Location;
            existing.ScheduledDate = intervention.ScheduledDate;
            existing.Priority = intervention.Priority;
            existing.Description = intervention.Description;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return existing;
        });

        return Task.FromResult(Store[intervention.Id]);
    }

    public Task<Intervention?> ChangeStatusAsync(Guid id, InterventionStatus status, CancellationToken cancellationToken)
    {
        if (!Store.TryGetValue(id, out var intervention))
        {
            return Task.FromResult<Intervention?>(null);
        }

        intervention.Status = status;
        intervention.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Task.FromResult<Intervention?>(intervention);
    }

    private static ConcurrentDictionary<Guid, Intervention> Seed()
    {
        var requester = "00000000-0000-0000-0000-000000000111";
        var values = new[]
        {
            new Intervention { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Title = "Network switch maintenance", Location = "Plant A", RequesterObjectId = requester, ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), Priority = InterventionPriority.High, Status = InterventionStatus.Submitted, Description = "Replace aging switch in control room.", UpdatedAtUtc = DateTimeOffset.UtcNow },
            new Intervention { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Title = "Safety inspection", Location = "Warehouse 4", RequesterObjectId = requester, ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), Priority = InterventionPriority.Medium, Status = InterventionStatus.Approved, Description = "Quarterly intervention readiness check.", UpdatedAtUtc = DateTimeOffset.UtcNow },
            new Intervention { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Title = "Cooling unit repair", Location = "Data room", RequesterObjectId = requester, ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)), Priority = InterventionPriority.Critical, Status = InterventionStatus.Draft, Description = "Intermittent cooling alarm requires intervention.", UpdatedAtUtc = DateTimeOffset.UtcNow }
        };

        return new ConcurrentDictionary<Guid, Intervention>(values.ToDictionary(x => x.Id));
    }
}
