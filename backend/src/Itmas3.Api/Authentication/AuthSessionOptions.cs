using System.ComponentModel.DataAnnotations;

namespace Itmas3.Api.Authentication;

/// <summary>Lifetime of the server-side authentication session (BFF cookie).</summary>
internal sealed class AuthSessionOptions
{
    public const string SectionName = "Authentication:Session";

    /// <summary>The session ends after this period without requests (sliding expiration).</summary>
    [Range(1, 1440)]
    public int IdleTimeoutMinutes { get; set; } = 60;

    /// <summary>The session ends after this period regardless of activity; the user must sign in again.</summary>
    [Range(1, 168)]
    public int AbsoluteTimeoutHours { get; set; } = 8;
}
