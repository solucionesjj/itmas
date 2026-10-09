using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Security;
using Itmas3.Application.Inventory.EnrollDevice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Itmas3.Api.Controllers.Inventory;

/// <summary>Enrollment of inventory agents with the authorization code (SPEC-0002 §8.1, ADR-0009).</summary>
[ApiController]
[Route("api/v1/device-enrollments")]
public sealed class DeviceEnrollmentsController(EnrollDeviceHandler handler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.DeviceEnrollment)]
    [EnableRateLimiting(RateLimitingPolicies.DeviceEnrollment)]
    [ProducesResponseType<DeviceEnrollmentResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Enroll(DeviceEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new EnrollDeviceCommand(
                request.HardwareId,
                request.Hostname,
                request.OsFamily,
                request.AgentVersion,
                request.TermsVersion,
                request.TermsAcceptedAt!.Value,
                ConnectionIp.Of(HttpContext)),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new DeviceEnrollmentResponse(result.DeviceId, result.ApiKey));
    }
}
