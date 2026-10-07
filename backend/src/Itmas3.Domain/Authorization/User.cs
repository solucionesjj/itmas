using Itmas3.Domain.Common;

namespace Itmas3.Domain.Authorization;

/// <summary>
/// Application user. Created without policies (no privileges) on first sign-in. Authentication data comes from the
/// identity provider; authorization data (policies, Root, active state) belongs to the application.
/// </summary>
public sealed class User : Entity<Guid>
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxEmailLength = 320;

    private readonly List<ExternalIdentity> _identities = [];
    private readonly List<PolicyAssignment> _policyAssignments = [];

    private User(Guid id, string displayName, string? email, DateTimeOffset createdAt)
        : base(id)
    {
        DisplayName = displayName;
        Email = email;
        CreatedAt = createdAt;
        IsActive = true;
        SecurityStamp = NewStamp();
    }

    public string DisplayName { get; private set; }

    public string? Email { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsRoot { get; private set; }

    /// <summary>Rotated whenever existing sessions must stop being valid (deactivation, loss of Root).</summary>
    public string SecurityStamp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastSignInAt { get; private set; }

    public IReadOnlyCollection<ExternalIdentity> Identities => _identities;

    public IReadOnlyCollection<PolicyAssignment> PolicyAssignments => _policyAssignments;

    public static User Provision(string provider, string issuer, string subject, string? displayName, string? email, DateTimeOffset now)
    {
        var user = new User(Guid.CreateVersion7(now), DisplayNameOrDefault(displayName, email), Truncate(email, MaxEmailLength), now);
        user._identities.Add(new ExternalIdentity(provider, issuer, subject));
        user.LastSignInAt = now;
        return user;
    }

    public void RecordSignIn(string? displayName, string? email, DateTimeOffset now)
    {
        DisplayName = DisplayNameOrDefault(displayName, email ?? Email);
        Email = Truncate(email, MaxEmailLength) ?? Email;
        LastSignInAt = now;
    }

    public bool SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        if (!isActive)
        {
            SecurityStamp = NewStamp();
        }

        return true;
    }

    public bool SetRoot(bool isRoot)
    {
        if (IsRoot == isRoot)
        {
            return false;
        }

        IsRoot = isRoot;
        return true;
    }

    public bool AssignPolicy(Guid policyId, Guid? assignedBy, DateTimeOffset now)
    {
        if (_policyAssignments.Any(assignment => assignment.PolicyId == policyId))
        {
            return false;
        }

        _policyAssignments.Add(new PolicyAssignment(policyId, assignedBy, now));
        return true;
    }

    public bool RevokePolicy(Guid policyId) =>
        _policyAssignments.RemoveAll(assignment => assignment.PolicyId == policyId) > 0;

    private static string DisplayNameOrDefault(string? displayName, string? email) =>
        Truncate(string.IsNullOrWhiteSpace(displayName) ? email : displayName, MaxDisplayNameLength) ?? "Usuario";

    private static string? Truncate(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
