using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Inventory;
using Itmas3.Api.Inventory;
using Itmas3.Api.Security;
using Itmas3.Application.Inventory;
using Itmas3.Application.Inventory.EnrollDevice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Itmas3.Api.Controllers.Inventory;

/// <summary>Enrollment of inventory agents with the one-time code of their registered device (SPEC-0003 §8.1, ADR-0012).</summary>
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
        // The device comes from the authenticated code, never from the payload.
        var deviceId = Guid.Parse(User.FindFirst(DeviceAuthenticationSchemes.EnrollmentClaim)!.Value);
        var codeHash = DeviceSecrets.HashEnrollmentCode(DeviceAuthenticationSchemes.ReadCredential(Request, DeviceAuthenticationSchemes.Enrollment))!;

        var result = await handler.HandleAsync(
            new EnrollDeviceCommand(
                deviceId,
                codeHash,
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
