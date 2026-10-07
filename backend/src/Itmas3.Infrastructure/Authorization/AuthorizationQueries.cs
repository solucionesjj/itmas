using Itmas3.Application.Authorization.Queries;
using Itmas3.Application.Common;
using Itmas3.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itmas3.Infrastructure.Authorization;

/// <summary>Read-only projections for the administration API (no tracking, parameterised SQL only).</summary>
internal sealed class AuthorizationQueries(AppDbContext db) : IAuthorizationQueries
{
    public async Task<PagedResult<UserSummary>> SearchUsersAsync(UserSearch search, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            query = query.Where(user => EF.Functions.ILike(user.DisplayName, pattern, LikePattern.Escape)
                || (user.Email != null && EF.Functions.ILike(user.Email, pattern, LikePattern.Escape)));
        }

        query = search.Status switch
        {
            UserStatusFilter.Active => query.Where(user => user.IsActive),
            UserStatusFilter.Inactive => query.Where(user => !user.IsActive),
            UserStatusFilter.WithoutPolicies => query.Where(user => !user.PolicyAssignments.Any()),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .Select(user => new UserSummary(
                user.Id, user.DisplayName, user.Email, user.IsActive, user.IsRoot, user.PolicyAssignments.Count(), user.CreatedAt, user.LastSignInAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummary>(items, search.Page.Page, search.Page.PageSize, total);
    }

    public async Task<UserDetail?> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DisplayName,
                candidate.Email,
                candidate.IsActive,
                candidate.IsRoot,
                candidate.CreatedAt,
                candidate.LastSignInAt,
                Assignments = candidate.PolicyAssignments.Select(assignment => new { assignment.PolicyId, assignment.AssignedAt }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var policyIds = user.Assignments.Select(assignment => assignment.PolicyId).ToList();
        var policies = await db.Policies.AsNoTracking()
            .Where(policy => policyIds.Contains(policy.Id))
            .Select(policy => new { policy.Id, policy.Name, policy.IsActive })
            .ToDictionaryAsync(policy => policy.Id, cancellationToken);

        var assigned = user.Assignments
            .Where(assignment => policies.ContainsKey(assignment.PolicyId))
            .Select(assignment => new AssignedPolicy(
                assignment.PolicyId, policies[assignment.PolicyId].Name, policies[assignment.PolicyId].IsActive, assignment.AssignedAt))
            .OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new UserDetail(user.Id, user.DisplayName, user.Email, user.IsActive, user.IsRoot, user.CreatedAt, user.LastSignInAt, assigned);
    }

    public async Task<PagedResult<PolicySummary>> SearchPoliciesAsync(PolicySearch search, CancellationToken cancellationToken)
    {
        var query = db.Policies.AsNoTracking();

        if (LikePattern.Contains(search.Text) is { } pattern)
        {
            query = query.Where(policy => EF.Functions.ILike(policy.Name, pattern, LikePattern.Escape)
                || EF.Functions.ILike(policy.Description, pattern, LikePattern.Escape));
        }

        if (search.IsActive is { } isActive)
        {
            query = query.Where(policy => policy.IsActive == isActive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(policy => policy.Name).ThenBy(policy => policy.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .Select(policy => new PolicySummary(
                policy.Id,
                policy.Name,
                policy.Description,
                policy.IsActive,
                policy.Statements.Count(),
                db.Users.Count(user => user.PolicyAssignments.Any(assignment => assignment.PolicyId == policy.Id)),
                policy.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PolicySummary>(items, search.Page.Page, search.Page.PageSize, total);
    }

    public async Task<PolicyDetail?> GetPolicyAsync(Guid id, CancellationToken cancellationToken)
    {
        var policy = await db.Policies.AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.IsActive,
                candidate.Revision,
                Permissions = candidate.Statements.Select(statement => statement.PermissionName).ToList(),
                UserCount = db.Users.Count(user => user.PolicyAssignments.Any(assignment => assignment.PolicyId == candidate.Id)),
                candidate.CreatedAt,
                candidate.UpdatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return policy is null
            ? null
            : new PolicyDetail(
                policy.Id, policy.Name, policy.Description, policy.IsActive, policy.Revision,
                policy.Permissions.Order(StringComparer.Ordinal).ToList(), policy.UserCount, policy.CreatedAt, policy.UpdatedAt);
    }

    public async Task<IReadOnlyList<UserReference>?> GetPolicyUsersAsync(Guid policyId, CancellationToken cancellationToken)
    {
        if (!await db.Policies.AnyAsync(policy => policy.Id == policyId, cancellationToken))
        {
            return null;
        }

        return await db.Users.AsNoTracking()
            .Where(user => user.PolicyAssignments.Any(assignment => assignment.PolicyId == policyId))
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserReference(user.Id, user.DisplayName, user.Email, user.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionSummary>> GetPermissionsAsync(CancellationToken cancellationToken) =>
        await db.Permissions.AsNoTracking()
            .OrderBy(permission => permission.Category).ThenBy(permission => permission.Id)
            .Select(permission => new PermissionSummary(
                permission.Id,
                permission.DisplayName,
                permission.Description,
                permission.Category,
                permission.IsGrantable,
                permission.IsDeprecated,
                db.Policies.Count(policy => policy.Statements.Any(statement => statement.PermissionName == permission.Id))))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssignedPolicy>?> GetPermissionPoliciesAsync(string permission, CancellationToken cancellationToken)
    {
        if (!await db.Permissions.AnyAsync(candidate => candidate.Id == permission, cancellationToken))
        {
            return null;
        }

        return await db.Policies.AsNoTracking()
            .Where(policy => policy.Statements.Any(statement => statement.PermissionName == permission))
            .OrderBy(policy => policy.Name)
            .Select(policy => new AssignedPolicy(policy.Id, policy.Name, policy.IsActive, null))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditEventSummary>> SearchAuditEventsAsync(AuditSearch search, CancellationToken cancellationToken)
    {
        var query = db.AuditEvents.AsNoTracking();

        if (search.From is { } from)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAt >= from);
        }

        if (search.To is { } to)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAt <= to);
        }

        if (search.ActorUserId is { } actor)
        {
            query = query.Where(auditEvent => auditEvent.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(search.Action))
        {
            query = query.Where(auditEvent => auditEvent.Action == search.Action);
        }

        if (!string.IsNullOrWhiteSpace(search.ResourceType))
        {
            query = query.Where(auditEvent => auditEvent.ResourceType == search.ResourceType);
        }

        if (!string.IsNullOrWhiteSpace(search.ResourceId))
        {
            query = query.Where(auditEvent => auditEvent.ResourceId == search.ResourceId);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(auditEvent => auditEvent.OccurredAt).ThenByDescending(auditEvent => auditEvent.Id)
            .Skip(search.Page.Skip).Take(search.Page.PageSize)
            .Select(auditEvent => new AuditEventSummary(
                auditEvent.Id,
                auditEvent.OccurredAt,
                auditEvent.ActorUserId,
                auditEvent.ActorDisplayName,
                auditEvent.Action,
                auditEvent.ResourceType,
                auditEvent.ResourceId,
                auditEvent.Result,
                auditEvent.Before,
                auditEvent.After,
                auditEvent.TraceId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEventSummary>(items, search.Page.Page, search.Page.PageSize, total);
    }
}

/// <summary>Builds ILIKE "contains" patterns with user input escaped (no wildcard injection).</summary>
internal static class LikePattern
{
    public const string Escape = "\\";

    public static string? Contains(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
