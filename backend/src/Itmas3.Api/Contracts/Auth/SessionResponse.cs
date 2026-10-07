namespace Itmas3.Api.Contracts.Auth;

/// <summary>
/// State of the browser session as seen by the BFF. Contains no tokens. <see cref="Permissions"/> lists the effective
/// permissions (empty for Root, who is authorised for everything) and is meant only to adapt the UI.
/// </summary>
public sealed record SessionResponse(bool IsAuthenticated, UserResponse? User, bool IsRoot, IReadOnlyList<string> Permissions)
{
    public static SessionResponse Anonymous { get; } = new(false, null, false, []);
}

public sealed record UserResponse(string DisplayName, string? Username);
