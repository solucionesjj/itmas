namespace Itmas3.Application.Authorization;

/// <summary>Audit action names (SPEC-0001 §14.3).</summary>
public static class AuditActions
{
    public const string UserProvisioned = "user.provisioned";
    public const string UserActivated = "user.activated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserPolicyAssigned = "user.policy.assigned";
    public const string UserPolicyRevoked = "user.policy.revoked";
    public const string RootBootstrapped = "root.bootstrapped";
    public const string RootGranted = "root.granted";
    public const string RootRevoked = "root.revoked";
    public const string PolicyCreated = "policy.created";
    public const string PolicyUpdated = "policy.updated";
    public const string PolicyActivated = "policy.activated";
    public const string PolicyDeactivated = "policy.deactivated";
    public const string PolicyDeleted = "policy.deleted";
    public const string PermissionUpdated = "permission.updated";
    public const string PermissionSynced = "permission.synced";
}

public static class AuditResourceTypes
{
    public const string User = "user";
    public const string Policy = "policy";
    public const string Permission = "permission";
}
