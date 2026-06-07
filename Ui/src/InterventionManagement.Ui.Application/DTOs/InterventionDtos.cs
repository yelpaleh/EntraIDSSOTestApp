namespace InterventionManagement.Ui.Application.DTOs;

public enum InterventionPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum InterventionStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Cancelled = 4,
    Completed = 5
}

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

public sealed record LocalLoginRequest(string Username, string Password);

public sealed record LocalLoginResponse(string AccessToken, string TokenType, int ExpiresIn);
