namespace InterventionManagement.Ui.Web.Models
{
    public sealed class AuthenticationFeaturesOptions
    {
        public const string SectionName = "AuthenticationFeatures";
        public bool EnableSSO { get; init; } = true;
        public bool EnableLocalLogin { get; init; } = false;
    }
}
