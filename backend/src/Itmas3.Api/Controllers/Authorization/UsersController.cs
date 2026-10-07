using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Abstractions;
using Itmas3.Application.Authorization.Queries;
using Itmas3.Application.Authorization.Users.AssignPolicy;
using Itmas3.Application.Authorization.Users.RevokePolicy;
using Itmas3.Application.Authorization.Users.SetRootStatus;
using Itmas3.Application.Authorization.Users.SetUserStatus;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Authorization;

/// <summary>Application users, their policies, effective permissions, state and Root status (SPEC-0001 §12).</summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IAuthorizationQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AuthorizationPermissions.UsersRead)]
    public async Task<PagedResponse<UserSummary>> Search(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, RegularExpression("^(all|active|inactive|without-policies)$")] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = status switch
        {
            "active" => UserStatusFilter.Active,
            "inactive" => UserStatusFilter.Inactive,
            "without-policies" => UserStatusFilter.WithoutPolicies,
            _ => UserStatusFilter.All,
        };

        var result = await queries.SearchUsersAsync(new UserSearch(search, filter, PagingDefaults.Page(page, pageSize)), cancellationToken);
        return new PagedResponse<UserSummary>(result.Items, result.Page, result.PageSize, result.Total);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AuthorizationPermissions.UsersRead)]
    public async Task<ActionResult<UserDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        await queries.GetUserAsync(id, cancellationToken) is { } user ? user : NotFound();

    [HttpGet("{id:guid}/effective-permissions")]
    [RequirePermission(AuthorizationPermissions.UsersRead)]
    public async Task<ActionResult<EffectivePermissionsResponse>> GetEffectivePermissions(
        Guid id, [FromServices] IAuthorizationSnapshotProvider snapshots, CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.LoadAsync(id, cancellationToken);
        if (snapshot is null)
        {
            return NotFound();
        }

        var permissions = snapshot.Grants
            .OrderBy(grant => grant.Key, StringComparer.Ordinal)
            .Select(grant => new EffectivePermissionResponse(grant.Key, grant.Value))
            .ToList();
        return new EffectivePermissionsResponse(snapshot.UserId, snapshot.IsActive, snapshot.IsRoot, permissions);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(AuthorizationPermissions.UsersUpdate)]
    public async Task<IActionResult> SetStatus(
        Guid id, UpdateUserStatusRequest request, [FromServices] SetUserStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetUserStatusCommand(id, request.IsActive!.Value), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/policies/{policyId:guid}")]
    [RequirePermission(AuthorizationPermissions.UsersPoliciesAssign)]
    public async Task<IActionResult> AssignPolicy(
        Guid id, Guid policyId, [FromServices] AssignPolicyHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new AssignPolicyCommand(id, policyId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/policies/{policyId:guid}")]
    [RequirePermission(AuthorizationPermissions.UsersPoliciesRevoke)]
    public async Task<IActionResult> RevokePolicy(
        Guid id, Guid policyId, [FromServices] RevokePolicyHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RevokePolicyCommand(id, policyId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/root")]
    [RequirePermission(AuthorizationPermissions.RootManage)]
    public async Task<IActionResult> GrantRoot(Guid id, [FromServices] SetRootStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetRootStatusCommand(id, true), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/root")]
    [RequirePermission(AuthorizationPermissions.RootManage)]
    public async Task<IActionResult> RevokeRoot(Guid id, [FromServices] SetRootStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetRootStatusCommand(id, false), cancellationToken);
        return NoContent();
    }
}
