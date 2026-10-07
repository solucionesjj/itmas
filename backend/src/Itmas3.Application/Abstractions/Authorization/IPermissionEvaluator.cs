using Itmas3.Application.Abstractions.Identity;

namespace Itmas3.Application.Abstractions.Authorization;

/// <summary>
/// Port that decides whether a user holds a permission. Implementations MUST deny by default:
/// return <c>true</c> only when the permission has been explicitly granted.
/// </summary>
public interface IPermissionEvaluator
{
    ValueTask<bool> IsGrantedAsync(ICurrentUser user, string permission, CancellationToken cancellationToken);
}
