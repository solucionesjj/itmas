namespace Itmas3.Application.Identity.GetCurrentUserProfile;

/// <summary>
/// Identity and effective permissions of the signed-in user. The client uses the permissions only to adapt the UI
/// (constitution §3.5); the API authorises every request independently.
/// </summary>
public sealed record CurrentUserProfile(string DisplayName, string? Username, bool IsRoot, IReadOnlyList<string> Permissions);
