using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Controllers.Authorization;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.ManageDevices;
using Itmas3.Application.Inventory.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Inventory;

/// <summary>
/// Device registry (SPEC-0003 §12.1). Updates use optimistic concurrency: GET returns an <c>ETag</c> and PUT requires
/// the matching <c>If-Match</c>. Responses that carry an enrollment code are never cached.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
public sealed class DevicesController(IInventoryQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(InventoryPermissions.DevicesRead)]
    public async Task<PagedResponse<DeviceListItem>> Search(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, RegularExpression(DeviceContract.StatusFilterPattern)] string? status,
        [FromQuery, RegularExpression(DeviceContract.TypePattern)] string? type,
        [FromQuery, RegularExpression(DeviceContract.SortPattern)] string? sort,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statusFilter = status switch
        {
            "pending" => DeviceStatusFilter.Pending,
            "active" => DeviceStatusFilter.Active,
            "deleted" => DeviceStatusFilter.Deleted,
            "all" => DeviceStatusFilter.All,
            _ => DeviceStatusFilter.Current,
        };
        var descending = sort?.StartsWith('-') == true;
        var sortBy = sort?.TrimStart('-') switch
        {
            "owner" => DeviceSort.Owner,
            "lastCollectedAt" => DeviceSort.LastCollectedAt,
            _ => DeviceSort.Name,
        };

        var result = await queries.SearchDevicesAsync(
            new DeviceSearch(search, statusFilter, type, sortBy, descending, PagingDefaults.Page(page, pageSize)), cancellationToken);
        return new PagedResponse<DeviceListItem>(result.Items, result.Page, result.PageSize, result.Total);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(InventoryPermissions.DevicesRead)]
    public async Task<ActionResult<DeviceDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await queries.GetDeviceAsync(id, cancellationToken) is not { } device)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(device.Revision);
        return device;
    }

    [HttpPost]
    [RequirePermission(InventoryPermissions.DevicesCreate)]
    [ProducesResponseType<DeviceCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CreateDeviceRequest request, [FromServices] RegisterDeviceHandler handler, CancellationToken cancellationToken)
    {
        var issued = await handler.HandleAsync(
            new RegisterDeviceCommand(request.Name, DeviceTypes.Parse(request.Type)!.Value, request.OwnerEmail), cancellationToken);

        var device = (await queries.GetDeviceAsync(issued.DeviceId, cancellationToken))!;
        Response.Headers.ETag = EntityTags.Format(device.Revision);
        Response.Headers.CacheControl = "no-store";
        return CreatedAtAction(
            nameof(Get),
            new { id = device.Id },
            new DeviceCreatedResponse(
                device.Id, device.Name, device.Type, device.Status, device.Owner, device.CreatedAt, issued.EnrollmentCode, issued.IssuedAt));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(InventoryPermissions.DevicesUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateDeviceRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        if (!EntityTags.TryParse(ifMatch, out var expectedRevision))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "Falta la cabecera If-Match con la versión del equipo.");
        }

        await handler.HandleAsync(
            new UpdateDeviceCommand(id, expectedRevision, request.Name, DeviceTypes.Parse(request.Type)!.Value, request.OwnerEmail),
            cancellationToken);

        var device = (await queries.GetDeviceAsync(id, cancellationToken))!;
        Response.Headers.ETag = EntityTags.Format(device.Revision);
        return Ok(device);
    }

    [HttpPost("{id:guid}/enrollment-code")]
    [RequirePermission(InventoryPermissions.DevicesUpdate)]
    [ProducesResponseType<EnrollmentCodeResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RegenerateEnrollmentCode(
        Guid id, [FromServices] RegenerateEnrollmentCodeHandler handler, CancellationToken cancellationToken)
    {
        var issued = await handler.HandleAsync(id, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status201Created, new EnrollmentCodeResponse(issued.EnrollmentCode, issued.IssuedAt));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(InventoryPermissions.DevicesDelete)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteDeviceHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }
}
