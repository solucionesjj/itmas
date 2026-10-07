using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Permissions.UpdatePermission;
using Itmas3.Application.Authorization.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Authorization;

/// <summary>Permission catalog (declared in code). Only presentation metadata is editable (SPEC-0001 RF-6..RF-9).</summary>
[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IAuthorizationQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AuthorizationPermissions.PermissionsRead)]
    public Task<IReadOnlyList<PermissionSummary>> List(CancellationToken cancellationToken) =>
        queries.GetPermissionsAsync(cancellationToken);

    [HttpGet("{name}/policies")]
    [RequirePermission(AuthorizationPermissions.PermissionsRead)]
    public async Task<ActionResult<IReadOnlyList<AssignedPolicy>>> GetPolicies(string name, CancellationToken cancellationToken) =>
        PermissionName.IsValid(name) && await queries.GetPermissionPoliciesAsync(name, cancellationToken) is { } policies
            ? Ok(policies)
            : NotFound();

    [HttpPatch("{name}")]
    [RequirePermission(AuthorizationPermissions.PermissionsUpdate)]
    public async Task<IActionResult> Update(
        string name, UpdatePermissionRequest request, [FromServices] UpdatePermissionHandler handler, CancellationToken cancellationToken)
    {
        if (!PermissionName.IsValid(name))
        {
            return NotFound();
        }

        await handler.HandleAsync(new UpdatePermissionCommand(name, request.DisplayName, request.Description, request.Category), cancellationToken);
        return NoContent();
    }
}
