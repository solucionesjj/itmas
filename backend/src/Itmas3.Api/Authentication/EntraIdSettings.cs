using Microsoft.Extensions.Configuration;

namespace Itmas3.Api.Authentication;

/// <summary>
/// Microsoft Entra ID settings. The whole section is consumed by Microsoft.Identity.Web.
/// Non-secret values may live in appsettings; the client credential MUST come from the environment
/// (<c>EntraId__ClientSecret</c>) or, preferably in production, from a certificate / managed identity
/// declared in <c>EntraId__ClientCredentials__*</c>.
/// </summary>
internal static class EntraIdSettings
{
    public const string SectionName = "EntraId";

    public static bool HasApplicationIdentity(IConfigurationSection section) =>
        !string.IsNullOrWhiteSpace(section["TenantId"]) && !string.IsNullOrWhiteSpace(section["ClientId"]);

    public static bool HasClientCredential(IConfigurationSection section) =>
        !string.IsNullOrWhiteSpace(section["ClientSecret"])
        || section.GetSection("ClientCredentials").GetChildren().Any();
}
