using Itmas3.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Itmas3.Api.Authorization;

/// <summary>
/// Protects an endpoint with an explicit permission (<c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>).
/// The caller must be authenticated AND be granted the permission by <c>IPermissionEvaluator</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = PermissionName.EnsureValid(permission);

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}
