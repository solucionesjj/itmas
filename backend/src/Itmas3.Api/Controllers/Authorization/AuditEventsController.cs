using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Application.Authorization;
using Itmas3.Application.Authorization.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Authorization;

/// <summary>Read-only access to the authorization audit trail (SPEC-0001 RF-21).</summary>
[ApiController]
[Route("api/v1/audit-events")]
public sealed class AuditEventsController(IAuthorizationQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AuthorizationPermissions.AuditRead)]
    public async Task<PagedResponse<AuditEventSummary>> Search(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? actorUserId,
        [FromQuery, StringLength(100)] string? action,
        [FromQuery, StringLength(50)] string? resourceType,
        [FromQuery, StringLength(200)] string? resourceId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.SearchAuditEventsAsync(
            new AuditSearch(from, to, actorUserId, action, resourceType, resourceId, PagingDefaults.Page(page, pageSize)), cancellationToken);
        return new PagedResponse<AuditEventSummary>(result.Items, result.Page, result.PageSize, result.Total);
    }
}
