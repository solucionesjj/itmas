namespace Itmas3.Api.Authorization;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Any signed-in user. ONLY for endpoints that operate on the caller's own identity/session
    /// (e.g. "who am I"). Never use it to protect application resources: use <see cref="RequirePermissionAttribute"/>.
    /// </summary>
    public const string AuthenticatedUser = "AuthenticatedUser";

    /// <summary>
    /// Device presenting the enrollment authorization code (ADR-0009). ONLY for <c>POST /api/v1/device-enrollments</c>.
    /// </summary>
    public const string DeviceEnrollment = "DeviceEnrollment";

    /// <summary>
    /// Enrolled device authenticated with its API key (ADR-0009). ONLY for device endpoints: a device never satisfies
    /// user policies or permissions, and a user session never satisfies this policy.
    /// </summary>
    public const string EnrolledDevice = "EnrolledDevice";
}
