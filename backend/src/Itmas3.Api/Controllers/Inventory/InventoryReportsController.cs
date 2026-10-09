using System.Text.Json;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Inventory;
using Itmas3.Api.Security;
using Itmas3.Application.Inventory.SubmitInventoryReport;
using Itmas3.Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Itmas3.Api.Controllers.Inventory;

/// <summary>Daily inventories sent by enrolled devices (SPEC-0002 §8.2).</summary>
[ApiController]
[Route("api/v1/inventory-reports")]
public sealed class InventoryReportsController(SubmitInventoryReportHandler handler) : ControllerBase
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.EnrolledDevice)]
    [EnableRateLimiting(RateLimitingPolicies.InventoryReports)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Submit(InventoryReportRequest request, CancellationToken cancellationToken)
    {
        // RN-1: the device is the authenticated principal, never a value of the payload.
        var deviceId = Guid.Parse(User.FindFirst(DeviceAuthenticationSchemes.DeviceIdClaim)!.Value);
        var publicNetwork = request.Network.PublicNetwork is { } declared
            ? new DeclaredPublicNetwork(declared.Ip, declared.Country, declared.Region, declared.City)
            : null;

        await handler.HandleAsync(
            new SubmitInventoryReportCommand(
                deviceId,
                request.CollectedAt!.Value,
                request.AgentVersion,
                ConnectionIp.Of(HttpContext),
                new SystemData(request.System.Hostname, request.System.OsFamily, request.System.OsName, request.System.OsVersion),
                publicNetwork,
                request.Hardware.Volumes?.Select(volume => new VolumeData(volume.IsSystem, volume.Encryption.Status)).ToList(),
                request.Security.Firewall?.Status,
                request.Accounts.Users?.Select(user => new AccountData(user.Enabled, user.IsAdmin)).ToList(),
                JsonSerializer.Serialize(request, PayloadOptions)),
            cancellationToken);

        return Accepted();
    }
}
