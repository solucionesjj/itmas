using System.Security.Claims;
using System.Text.Encodings.Web;
using Itmas3.Application.Inventory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Itmas3.Api.Inventory;

/// <summary>
/// Validates the deployment-wide authorization code presented by an agent that wants to enroll (ADR-0009).
/// Constant-time comparison. When enrollment is disabled (no code configured) the challenge answers 404.
/// </summary>
internal sealed partial class EnrollmentCodeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<InventoryOptions> inventoryOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = inventoryOptions.Value.EnrollmentCode;
        var provided = DeviceAuthenticationSchemes.ReadCredential(Request, DeviceAuthenticationSchemes.Enrollment);

        if (string.IsNullOrEmpty(configured) || provided is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!DeviceSecrets.FixedTimeEquals(provided, configured))
        {
            LogInvalidEnrollmentCode(Logger, Context.Connection.RemoteIpAddress?.ToString());
            return Task.FromResult(AuthenticateResult.Fail("Invalid enrollment code."));
        }

        var identity = new ClaimsIdentity([new Claim(DeviceAuthenticationSchemes.EnrollmentClaim, "true")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!inventoryOptions.Value.EnrollmentEnabled)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        Response.Headers.WWWAuthenticate = DeviceAuthenticationSchemes.Enrollment;
        return base.HandleChallengeAsync(properties);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device enrollment rejected: invalid authorization code from {RemoteIp}")]
    private static partial void LogInvalidEnrollmentCode(ILogger logger, string? remoteIp);
}
