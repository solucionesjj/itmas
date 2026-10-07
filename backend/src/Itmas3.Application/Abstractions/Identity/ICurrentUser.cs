namespace Itmas3.Application.Abstractions.Identity;

/// <summary>
/// Identity of the caller of the current operation, resolved by the presentation layer
/// from the validated authentication session. Never trust identity data sent by the client.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Internal application user id, present once the user has been provisioned at sign-in.</summary>
    Guid? ApplicationUserId { get; }

    /// <summary>Stable, tenant-scoped user identifier (Microsoft Entra ID object id, <c>oid</c>).</summary>
    string? UserId { get; }

    /// <summary>Microsoft Entra ID tenant identifier (<c>tid</c>).</summary>
    string? TenantId { get; }

    string? DisplayName { get; }

    /// <summary>Sign-in name (<c>preferred_username</c>). Display only: it is mutable and MUST NOT be used as a key.</summary>
    string? Username { get; }
}
