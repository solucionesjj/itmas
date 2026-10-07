using System.Security.Claims;
using Itmas3.Application.Abstractions.Identity;

namespace Itmas3.Api.Authentication;

/// <summary>Adapts the validated <see cref="ClaimsPrincipal"/> of the session to <see cref="ICurrentUser"/>.</summary>
internal sealed class ClaimsCurrentUser(ClaimsPrincipal? principal) : ICurrentUser
{
    private static readonly string[] ObjectIdClaims = ["oid", "http://schemas.microsoft.com/identity/claims/objectidentifier"];
    private static readonly string[] TenantIdClaims = ["tid", "http://schemas.microsoft.com/identity/claims/tenantid"];

    private readonly ClaimsPrincipal _principal = principal ?? new ClaimsPrincipal(new ClaimsIdentity());

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;

    public Guid? ApplicationUserId =>
        Guid.TryParse(Find([ApplicationClaimTypes.UserId]), out var id) ? id : null;

    public string? SecurityStamp => Find([ApplicationClaimTypes.SecurityStamp]);

    public string? UserId => Find(ObjectIdClaims);

    public string? TenantId => Find(TenantIdClaims);

    public string? DisplayName => Find(["name"]);

    public string? Username => Find(["preferred_username"]);

    private string? Find(string[] claimTypes)
    {
        if (!IsAuthenticated)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = _principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
