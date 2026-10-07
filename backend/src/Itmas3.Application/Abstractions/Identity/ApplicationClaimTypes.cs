namespace Itmas3.Application.Abstractions.Identity;

/// <summary>Claims added to the session by the application at sign-in (never issued by the identity provider).</summary>
public static class ApplicationClaimTypes
{
    public const string UserId = "itmas3_uid";
    public const string SecurityStamp = "itmas3_sstamp";
}
