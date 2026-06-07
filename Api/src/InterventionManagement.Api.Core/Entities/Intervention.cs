using InterventionManagement.Api.Core.Enums;

namespace InterventionManagement.Api.Core.Entities;

public sealed class Intervention
{
    public Guid Id { get; init; }
    public required string Title { get; set; }
    public required string Location { get; set; }
    public required string RequesterObjectId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public InterventionPriority Priority { get; set; }
    public InterventionStatus Status { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
