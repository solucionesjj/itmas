namespace Itmas3.Api.Authorization;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Any signed-in user. ONLY for endpoints that operate on the caller's own identity/session
    /// (e.g. "who am I"). Never use it to protect application resources: use <see cref="RequirePermissionAttribute"/>.
    /// </summary>
    public const string AuthenticatedUser = "AuthenticatedUser";
}
