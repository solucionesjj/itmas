using Itmas3.Application.Common;

namespace Itmas3.Application.Authorization.Queries;

/// <summary>
/// Read side of the authorization module (read-only projections, no domain rules). Query use cases are exposed
/// directly as this port; the implementation lives in Infrastructure.
/// </summary>
public interface IAuthorizationQueries
{
    Task<PagedResult<UserSummary>> SearchUsersAsync(UserSearch search, CancellationToken cancellationToken);

    Task<UserDetail?> GetUserAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PolicySummary>> SearchPoliciesAsync(PolicySearch search, CancellationToken cancellationToken);

    Task<PolicyDetail?> GetPolicyAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserReference>?> GetPolicyUsersAsync(Guid policyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionSummary>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AssignedPolicy>?> GetPermissionPoliciesAsync(string permission, CancellationToken cancellationToken);

    Task<PagedResult<AuditEventSummary>> SearchAuditEventsAsync(AuditSearch search, CancellationToken cancellationToken);
}

public enum UserStatusFilter
{
    All,
    Active,
    Inactive,
    WithoutPolicies,
}

public sealed record UserSearch(string? Text, UserStatusFilter Status, PageRequest Page);

public sealed record UserSummary(
    Guid Id, string DisplayName, string? Email, bool IsActive, bool IsRoot, int PolicyCount, DateTimeOffset CreatedAt, DateTimeOffset? LastSignInAt);

public sealed record AssignedPolicy(Guid Id, string Name, bool IsActive, DateTimeOffset? AssignedAt);

public sealed record UserDetail(
    Guid Id, string DisplayName, string? Email, bool IsActive, bool IsRoot, DateTimeOffset CreatedAt, DateTimeOffset? LastSignInAt,
    IReadOnlyList<AssignedPolicy> Policies);

public sealed record UserReference(Guid Id, string DisplayName, string? Email, bool IsActive);

public sealed record PolicySearch(string? Text, bool? IsActive, PageRequest Page);

public sealed record PolicySummary(
    Guid Id, string Name, string Description, bool IsActive, int PermissionCount, int UserCount, DateTimeOffset UpdatedAt);

public sealed record PolicyDetail(
    Guid Id, string Name, string Description, bool IsActive, int Revision, IReadOnlyList<string> Permissions, int UserCount,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record PermissionSummary(
    string Name, string DisplayName, string Description, string Category, bool IsGrantable, bool IsDeprecated, int PolicyCount);

public sealed record AuditSearch(
    DateTimeOffset? From, DateTimeOffset? To, Guid? ActorUserId, string? Action, string? ResourceType, string? ResourceId, PageRequest Page);

public sealed record AuditEventSummary(
    Guid Id, DateTimeOffset OccurredAt, Guid? ActorUserId, string ActorDisplayName, string Action, string ResourceType,
    string? ResourceId, string Result, string? Before, string? After, string? TraceId);
