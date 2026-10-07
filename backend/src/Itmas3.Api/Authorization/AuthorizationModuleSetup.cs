using Itmas3.Application.Authorization;

namespace Itmas3.Api.Authorization;

/// <summary>Composition of the policy-based authorization module (SPEC-0001).</summary>
internal static class AuthorizationModuleSetup
{
    public const string SectionName = "Authorization";

    public static IServiceCollection AddAuthorizationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        // Accepts "id1,id2" (single environment variable) or an array (BootstrapRootObjectIds__0, __1, ...).
        var bootstrapIds = (section["BootstrapRootObjectIds"] ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(section.GetSection("BootstrapRootObjectIds").GetChildren().Select(child => child.Value ?? string.Empty))
            .Where(value => value.Length > 0)
            .ToList();

        var invalid = bootstrapIds.Where(value => !Guid.TryParse(value, out _)).ToList();
        if (invalid.Count > 0)
        {
            throw new InvalidOperationException("Authorization:BootstrapRootObjectIds must contain Microsoft Entra ID object ids (GUIDs).");
        }

        services.AddSingleton(new AuthorizationSettings
        {
            BootstrapRootObjectIds = bootstrapIds.ToHashSet(StringComparer.OrdinalIgnoreCase),
            AllowGuestUsers = section.GetValue<bool>("AllowGuestUsers"),
        });

        services.AddHostedService<PermissionCatalogSynchronizer>();
        return services;
    }
}
