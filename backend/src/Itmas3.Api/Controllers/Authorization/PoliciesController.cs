using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Policies.CreatePolicy;
using Itmas3.Application.Authorization.Policies.DeletePolicy;
using Itmas3.Application.Authorization.Policies.UpdatePolicy;
using Itmas3.Application.Authorization.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Authorization;

/// <summary>
/// Policies: reusable sets of permissions (SPEC-0001 §12). Updates use optimistic concurrency:
/// GET returns an <c>ETag</c> and PUT requires the matching <c>If-Match</c>.
/// </summary>
[ApiController]
[Route("api/v1/policies")]
public sealed class PoliciesController(IAuthorizationQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AuthorizationPermissions.PoliciesRead)]
    public async Task<PagedResponse<PolicySummary>> Search(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, RegularExpression("^(all|active|inactive)$")] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        bool? isActive = status switch { "active" => true, "inactive" => false, _ => null };
        var result = await queries.SearchPoliciesAsync(new PolicySearch(search, isActive, PagingDefaults.Page(page, pageSize)), cancellationToken);
        return new PagedResponse<PolicySummary>(result.Items, result.Page, result.PageSize, result.Total);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AuthorizationPermissions.PoliciesRead)]
    public async Task<ActionResult<PolicyDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await queries.GetPolicyAsync(id, cancellationToken) is not { } policy)
        {
            return NotFound();
        }

        SetETag(policy.Revision);
        return policy;
    }

    [HttpGet("{id:guid}/users")]
    [RequirePermission(AuthorizationPermissions.PoliciesRead)]
    public async Task<ActionResult<IReadOnlyList<UserReference>>> GetUsers(Guid id, CancellationToken cancellationToken) =>
        await queries.GetPolicyUsersAsync(id, cancellationToken) is { } users ? Ok(users) : NotFound();

    [HttpPost]
    [RequirePermission(AuthorizationPermissions.PoliciesCreate)]
    public async Task<IActionResult> Create(
        CreatePolicyRequest request, [FromServices] CreatePolicyHandler handler, CancellationToken cancellationToken)
    {
        var id = await handler.HandleAsync(new CreatePolicyCommand(request.Name, request.Description, request.Permissions), cancellationToken);
        var policy = await queries.GetPolicyAsync(id, cancellationToken);
        SetETag(policy!.Revision);
        return CreatedAtAction(nameof(Get), new { id }, policy);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AuthorizationPermissions.PoliciesUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdatePolicyRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdatePolicyHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryParseETag(ifMatch, out var expectedRevision))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "Falta la cabecera If-Match con la versión de la política.");
        }

        await handler.HandleAsync(
            new UpdatePolicyCommand(id, expectedRevision, request.Name, request.Description, request.IsActive!.Value, request.Permissions),
            cancellationToken);

        var policy = await queries.GetPolicyAsync(id, cancellationToken);
        SetETag(policy!.Revision);
        return Ok(policy);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AuthorizationPermissions.PoliciesDelete)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeletePolicyHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    private void SetETag(int revision) => Response.Headers.ETag = $"\"{revision.ToString(CultureInfo.InvariantCulture)}\"";

    private static bool TryParseETag(string? value, out int revision)
    {
        revision = 0;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        return int.TryParse(trimmed.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out revision);
    }
}
