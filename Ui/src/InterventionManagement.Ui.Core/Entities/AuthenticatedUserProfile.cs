namespace InterventionManagement.Ui.Core.Entities;

public sealed record AuthenticatedUserProfile(
    string DisplayName,
    string Email,
    string? PhotoDataUrl,
    IReadOnlyCollection<string> Roles);
