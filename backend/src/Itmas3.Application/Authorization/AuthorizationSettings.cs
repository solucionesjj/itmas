namespace Itmas3.Application.Authorization;

/// <summary>Deployment settings of the authorization module (bound by the composition root).</summary>
public sealed class AuthorizationSettings
{
    /// <summary>
    /// Entra ID object ids (<c>oid</c>) that become Root at sign-in ONLY while no active Root exists
    /// (initial installation and emergency recovery).
    /// </summary>
    public IReadOnlySet<string> BootstrapRootObjectIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allow B2B guest accounts of the tenant to be provisioned. Off by default (SPEC-0001 P2).</summary>
    public bool AllowGuestUsers { get; init; }
}
