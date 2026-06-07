using System.ComponentModel.DataAnnotations;
using InterventionManagement.Ui.Application.DTOs;

namespace InterventionManagement.Ui.Web.Models;

public sealed class InterventionFormViewModel
{
    public Guid? Id { get; init; }

    [Required, StringLength(160)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(120)]
    public string Location { get; init; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly ScheduledDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    public InterventionPriority Priority { get; init; } = InterventionPriority.Medium;

    [StringLength(2000)]
    public string? Description { get; init; }

    public static InterventionFormViewModel FromDto(InterventionDto intervention) =>
        new()
        {
            Id = intervention.Id,
            Title = intervention.Title,
            Location = intervention.Location,
            ScheduledDate = intervention.ScheduledDate,
            Priority = intervention.Priority,
            Description = intervention.Description
        };

    public UpsertInterventionRequest ToRequest() =>
        new(Title, Location, ScheduledDate, Priority, Description);
}
