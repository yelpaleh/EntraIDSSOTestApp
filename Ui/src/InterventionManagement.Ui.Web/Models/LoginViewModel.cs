namespace InterventionManagement.Ui.Web.Models;

public sealed class LoginViewModel
{
    public bool EnableSSO { get; init; }
    public bool EnableLocalLogin { get; init; }
    public string? ReturnUrl { get; init; }
    public string? ErrorMessage { get; init; }
    public string Username { get; init; } = string.Empty;
}
