using InterventionManagement.Api.Core.Enums;

namespace InterventionManagement.Api.Application.DTOs;

public sealed record InterventionDto(
    Guid Id,
    string Title,
    string Location,
    string RequesterObjectId,
    DateOnly ScheduledDate,
    InterventionPriority Priority,
    InterventionStatus Status,
    string? Description,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertInterventionRequest(
    string Title,
    string Location,
    DateOnly ScheduledDate,
    InterventionPriority Priority,
    string? Description);

public sealed record InterventionActionRequest(string Action);

public sealed record CalendarEventDto(Guid Id, string Title, DateOnly Start, string Status);

public sealed record EmailNotificationSettingsDto(bool Enabled, string SenderMailbox, string NotificationAlias);

public sealed record UserAuthorizationDto(string ObjectId, IReadOnlyCollection<string> Roles);
