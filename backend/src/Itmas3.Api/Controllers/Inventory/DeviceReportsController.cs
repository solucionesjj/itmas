using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Controllers.Authorization;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Inventory;

/// <summary>
/// Inventory collected from a device: its history and its reports (SPEC-0003 RF-15..RF-17). These contain local accounts,
/// IPs and location, so they require their own permission.
/// </summary>
[ApiController]
[Route("api/v1/devices/{deviceId:guid}/reports")]
public sealed class DeviceReportsController(IInventoryQueries queries) : ControllerBase
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [RequirePermission(InventoryPermissions.DevicesReportsRead)]
    public async Task<ActionResult<PagedResponse<DeviceReportListItem>>> List(
        Guid deviceId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, PagingDefaults.MaxPageSize)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (await queries.GetDeviceReportsAsync(deviceId, PagingDefaults.Page(page, pageSize), cancellationToken) is not { } result)
        {
            return NotFound();
        }

        return new PagedResponse<DeviceReportListItem>(result.Items, result.Page, result.PageSize, result.Total);
    }

    [HttpGet("latest")]
    [RequirePermission(InventoryPermissions.DevicesReportsRead)]
    public Task<ActionResult<DeviceReportResponse>> Latest(Guid deviceId, CancellationToken cancellationToken) =>
        ReportAsync(deviceId, null, cancellationToken);

    [HttpGet("{reportId:guid}")]
    [RequirePermission(InventoryPermissions.DevicesReportsRead)]
    public Task<ActionResult<DeviceReportResponse>> Get(Guid deviceId, Guid reportId, CancellationToken cancellationToken) =>
        ReportAsync(deviceId, reportId, cancellationToken);

    private async Task<ActionResult<DeviceReportResponse>> ReportAsync(Guid deviceId, Guid? reportId, CancellationToken cancellationToken)
    {
        if (await queries.GetDeviceReportAsync(deviceId, reportId, cancellationToken) is not { } report)
        {
            return NotFound();
        }

        // The payload was validated against this contract when it arrived (SPEC-0002 §12.2).
        var inventory = JsonSerializer.Deserialize<InventoryReportRequest>(report.PayloadJson, PayloadOptions)!;
        return new DeviceReportResponse(report.Id, report.Device, report.CollectedAt, report.ReceivedAt, report.ConnectionIp, report.IsLatest, inventory);
    }
}
